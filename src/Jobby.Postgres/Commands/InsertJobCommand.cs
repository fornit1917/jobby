using Jobby.Core.Models;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal static class InsertJobCommand
{
    private const string CommandText = @"
        INSERT INTO jobby_jobs (
            id,
            job_name,
            job_param,
            status,
            created_at,
            scheduled_start_at,
            cron,
            next_job_id
        )
        VALUES (
            $1,
            $2,
            $3,
            $4,
            $5,
            $6,
            $7,
            $8
        )
        ON CONFLICT (job_name) WHERE cron IS NOT null DO 
        UPDATE SET
	        cron = $7,
	        scheduled_start_at = $6;
    ";

    private static NpgsqlCommand CreateCommand(NpgsqlConnection conn, Job job)
    {
        return new NpgsqlCommand(CommandText, conn)
        {
            Parameters =
            {
                new() { Value = job.Id },                                           // 1
                new() { Value = job.JobName },                                      // 2
                new() { Value = (object?)job.JobParam ?? DBNull.Value },            // 3
                new() { Value = (int)job.Status },                                  // 4
                new() { Value = job.CreatedAt },                                    // 5
                new() { Value = job.ScheduledStartAt },                             // 6
                new() { Value = (object?)job.Cron ?? DBNull.Value },                // 7
                new() { Value = (object?)job.NextJobId ?? DBNull.Value },           // 8
            }
        };
    }

    public static async Task ExecuteAsync(NpgsqlConnection conn, Job job)
    {
        await using var cmd = CreateCommand(conn, job);
        await cmd.ExecuteNonQueryAsync();
    }

    public static void Execute(NpgsqlConnection conn, Job job)
    {
        using var cmd = CreateCommand(conn, job);
        cmd.ExecuteNonQuery();
    }
}
