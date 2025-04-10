using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal static class TakeBatchToProcessingCommand
{
    private static readonly string TakeBatchToProcessingSql = $@"
        WITH ready_jobs AS (
	        SELECT id FROM jobby_jobs 
	        WHERE
                status = {(int)JobStatus.Scheduled}
                AND scheduled_start_at <= $1
	        ORDER BY scheduled_start_at
	        LIMIT $2
	        FOR UPDATE SKIP LOCKED
        )
        UPDATE jobby_jobs
        SET
	        status = {(int)JobStatus.Processing},
	        last_started_at = $1,
	        started_count = started_count + 1
        WHERE id IN (SELECT id FROM ready_jobs)
        RETURNING *;
    ";

    public static async Task ExecuteAndWriteToListAsync(NpgsqlConnection conn, DateTime now, int maxBatchSize, List<Job> result)
    {
        result.Clear();

        await using var cmd = new NpgsqlCommand(TakeBatchToProcessingSql, conn)
        {
            Parameters =
            {
                new() { Value = now },              // 1
                new() { Value = maxBatchSize }      // 2
            }
        };

        var reader = await cmd.ExecuteReaderAsync();
        
        while (true)
        {
            var job = await reader.GetJobAsync();
            if (job == null)
            {
                return;
            }

            result.Add(job);
        }
    }
}
