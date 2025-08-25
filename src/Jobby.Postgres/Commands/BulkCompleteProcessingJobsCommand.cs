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
            UPDATE {TableName.Jobs(settings)}
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
	            UPDATE jobby_jobs 
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
            UPDATE jobby_jobs 
            SET
	            status = {(int)JobStatus.Scheduled}
            WHERE
                id IN (SELECT next_job_id FROM complete_and_get_next_job_id)
                AND id = ANY($4)
                AND status = {(int)JobStatus.WaitingPrev}
        ";
    }

    public async Task ExecuteAsync(ProcessingJobsList jobs, IReadOnlyList<Guid>? nextJobIds = null)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        if (nextJobIds is { Count: > 0 })
        {
            await using var completeAndUnlockNextCmd = new NpgsqlCommand(_completeAndUnlockNextCommandText, conn)
            {
                Parameters =
                {
                    new() { Value = DateTime.UtcNow },
                    new() { Value = jobs.JobIds },
                    new() { Value = jobs.ServerId },
                    new() { Value = nextJobIds }
                }
            };

            await completeAndUnlockNextCmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var updateCmd = new NpgsqlCommand(_completeCommandText, conn)
            {
                Parameters =
                {
                    new() { Value = DateTime.UtcNow },
                    new() { Value = jobs.JobIds },
                    new() { Value = jobs.ServerId }
                }
            };

            await updateCmd.ExecuteNonQueryAsync();
        }
    }
}
