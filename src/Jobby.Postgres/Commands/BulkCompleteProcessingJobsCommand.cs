using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class BulkCompleteProcessingJobsCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _completeCommandText;
    private readonly string _completeAndUnlockNextCommandText;

    public BulkCompleteProcessingJobsCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _completeCommandText = @$"
            UPDATE {DbName.Jobs(settings)}
            SET
                status = {(int)JobStatus.Completed},
                error = null,
                last_finished_at = $1
            WHERE
                id = ANY($2)
                AND status = {(int)JobStatus.Processing}
                AND server_id = $3
        ";

        _completeAndUnlockNextCommandText = @$"
            WITH complete_and_get_next_job_id AS (
	            UPDATE {DbName.Jobs(settings)} 
	            SET
		            status = {(int)JobStatus.Completed},
		            last_finished_at = $1,
		            error = NULL
	            WHERE 
		            id = ANY($2)
		            AND status = {(int)JobStatus.Processing}
		            AND server_id = $3
	            RETURNING next_job_id 
            )
            UPDATE {DbName.Jobs(settings)} 
            SET
	            status = {(int)JobStatus.Scheduled}
            WHERE
                id IN (SELECT next_job_id FROM complete_and_get_next_job_id)
                AND id = ANY($4)
                AND status = {(int)JobStatus.WaitingPrev}
        ";
    }

    public async Task ExecuteAsync(CompleteJobsBatch jobs)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        if (jobs.NextJobIds is { Count: > 0 })
        {
            await using var completeAndUnlockNextCmd = new NpgsqlCommand(_completeAndUnlockNextCommandText, conn);
            completeAndUnlockNextCmd.Parameters.Add(new() { Value = DateTime.UtcNow });
            completeAndUnlockNextCmd.Parameters.Add(new() { Value = jobs.JobIds });
            completeAndUnlockNextCmd.Parameters.Add(new() { Value = jobs.ServerId });
            completeAndUnlockNextCmd.Parameters.Add(new() { Value = jobs.NextJobIds });

            await completeAndUnlockNextCmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var updateCmd = new NpgsqlCommand(_completeCommandText, conn);
            updateCmd.Parameters.Add(new() { Value = DateTime.UtcNow });
            updateCmd.Parameters.Add(new() { Value = jobs.JobIds });
            updateCmd.Parameters.Add(new() { Value = jobs.ServerId });

            await updateCmd.ExecuteNonQueryAsync();
        }
    }
}
