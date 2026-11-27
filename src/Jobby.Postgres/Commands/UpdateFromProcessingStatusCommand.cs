using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class UpdateFromProcessingStatusCommand
{
    private readonly NpgsqlDataSource _dataSource;

    private readonly string _updateStatusCommandText;
    private readonly string _updateAndUnlockNextCommandText;

    public UpdateFromProcessingStatusCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _updateStatusCommandText = $@"
            UPDATE {DbName.Jobs(settings)}
            SET
                status = $1,
                last_finished_at = now(),
                error = $2
            WHERE
                id = $3
                AND status = {(int)JobStatus.Processing}
                AND server_id = $4
        ";

        _updateAndUnlockNextCommandText = @$"
            WITH complete_and_get_next_job_id AS (
	            UPDATE {DbName.Jobs(settings)}
	            SET
		            status = $1,
		            last_finished_at = now(),
		            error = $2
	            WHERE
		            id = $3
		            AND status = {(int)JobStatus.Processing}
		            AND server_id = $4
	            RETURNING next_job_id
            )
            UPDATE {DbName.Jobs(settings)}
            SET
	            status = {(int)JobStatus.Scheduled}
            WHERE
                id IN (SELECT next_job_id FROM complete_and_get_next_job_id)
                AND id = $5
                AND status = {(int)JobStatus.WaitingPrev}
        ";
    }

    public async Task ExecuteAsync(JobExecutionModel job, JobStatus newStatus, string? error)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        if (job.NextJobId == null || newStatus != JobStatus.Completed)
        {
            await using var cmd = new NpgsqlCommand(_updateStatusCommandText, conn);
            cmd.Parameters.Add(new() { Value = (int)newStatus });                  // 1
            cmd.Parameters.Add(new() { Value = error as object ?? DBNull.Value }); // 2
            cmd.Parameters.Add(new() { Value = job.Id });                          // 3
            cmd.Parameters.Add(new() { Value = job.ServerId });                    // 4
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var updateAndUnlockNextCmd = new NpgsqlCommand(_updateAndUnlockNextCommandText, conn);
            updateAndUnlockNextCmd.Parameters.Add(new() { Value = (int)newStatus }); // 1
            updateAndUnlockNextCmd.Parameters.Add(new() { Value = error as object ?? DBNull.Value }); // 2
            updateAndUnlockNextCmd.Parameters.Add(new() { Value = job.Id });         // 3
            updateAndUnlockNextCmd.Parameters.Add(new() { Value = job.ServerId });   // 4
            updateAndUnlockNextCmd.Parameters.Add(new() { Value = job.NextJobId });  // 5
            await updateAndUnlockNextCmd.ExecuteNonQueryAsync();
        }
    }
}
