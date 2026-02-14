using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class DeleteProcessingJobCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _deleteCommandText;
    private readonly string _deleteAndUnlockNextCommandText;

    public DeleteProcessingJobCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;
        
        _deleteCommandText = @$"
            DELETE FROM {DbName.Jobs(settings)}
            WHERE
                id = $1
                AND status = {(int)JobStatus.Processing}
                AND server_id = $2";

        _deleteAndUnlockNextCommandText = $@"
            WITH complete_and_get_next_job_id AS (
	            DELETE FROM {DbName.Jobs(settings)} 
	            WHERE 
		            id = $1
		            AND status = {(int)JobStatus.Processing}
		            AND server_id = $2
	            RETURNING next_job_id
            )
            UPDATE {DbName.Jobs(settings)}
            SET
	            status = {(int)JobStatus.Scheduled}
            WHERE
                id IN (SELECT next_job_id FROM complete_and_get_next_job_id)
                AND id = $3
                AND status = {(int)JobStatus.WaitingPrev}
        ";
    }

    public async Task ExecuteAsync(JobExecutionModel job)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        if (job.NextJobId == null)
        {
            await using var cmd = new NpgsqlCommand(_deleteCommandText, conn);
            cmd.Parameters.Add(new() { Value = job.Id });
            cmd.Parameters.Add(new() { Value = job.ServerId });
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var deleteAndUnlockNextCmd = new NpgsqlCommand(_deleteAndUnlockNextCommandText, conn);
            deleteAndUnlockNextCmd.Parameters.Add(new() { Value = job.Id });
            deleteAndUnlockNextCmd.Parameters.Add(new() { Value = job.ServerId });
            deleteAndUnlockNextCmd.Parameters.Add(new() { Value = job.NextJobId });

            await deleteAndUnlockNextCmd.ExecuteNonQueryAsync();
        }
    }
}
