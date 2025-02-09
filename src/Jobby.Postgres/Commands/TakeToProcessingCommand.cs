using Jobby.Abstractions.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal static class TakeToProcessingCommand
{
    private static readonly string CommandText = $@"
        WITH ready_job AS (
	        SELECT id FROM jobby_jobs 
	        WHERE
                status = {(int)JobStatus.Scheduled}
                AND scheduled_start_at <= @now
	        ORDER BY scheduled_start_at
	        LIMIT 1
	        FOR UPDATE SKIP LOCKED
        )
        UPDATE jobby_jobs
        SET
	        status = {(int)JobStatus.Processing},
	        last_started_at = @now,
	        started_count = started_count + 1
        WHERE id IN (SELECT id FROM ready_job)
        RETURNING *;
    ";

    public static async Task<JobModel?> ExecuteAsync(NpgsqlConnection conn, DateTime now)
    {
        await using var cmd = new NpgsqlCommand(CommandText, conn)
        {
            Parameters =
            {
                new("now", now) 
            }
        };

        var reader = await cmd.ExecuteReaderAsync();

        return await reader.GetJobAsync();
    }
}
