using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class BulkDeleteProcessingJobsCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _deleteCommandText;
    private readonly string _deleteAndUnlockNextCommandText;

    public BulkDeleteProcessingJobsCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _deleteCommandText = @$"
            DELETE FROM {TableName.Jobs(settings)}
            WHERE
                id = ANY($1)
                AND status={(int)JobStatus.Processing}
                AND server_id = $2
        ";

        _deleteAndUnlockNextCommandText = $@"
            WITH complete_and_get_next_job_id AS (
	            DELETE FROM jobby_jobs 
	            WHERE 
		            id = ANY($1)
		            AND status = {(int)JobStatus.Processing}
		            AND server_id = $2
	            RETURNING next_job_id
            )
            UPDATE jobby_jobs
            SET
	            status = {(int)JobStatus.Scheduled}
            WHERE
                id IN (SELECT next_job_id FROM complete_and_get_next_job_id)
                AND id = ANY($3)
                AND status = {(int)JobStatus.WaitingPrev}
        ";
    }

    public async Task ExecuteAsync(ProcessingJobsList jobs, IReadOnlyList<Guid>? nextJobIds = null)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        if (nextJobIds is { Count : > 0})
        {
            await using var deleteAndUnlockNextCmd = new NpgsqlCommand(_deleteAndUnlockNextCommandText, conn)
            {
                Parameters =
                {
                    new() { Value = jobs.JobIds },
                    new() { Value = jobs.ServerId },
                    new() { Value = nextJobIds }
                }
            };

            await deleteAndUnlockNextCmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var deleteCmd = new NpgsqlCommand(_deleteCommandText, conn)
            {
                Parameters =
                {
                    new() { Value = jobs.JobIds },
                    new() { Value = jobs.ServerId },
                }
            };

            await deleteCmd.ExecuteNonQueryAsync();
        }
    }
}
