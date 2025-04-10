using Jobby.Core.Models;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal static class BulkInsertJobsCommand
{
    public const string CommandText = @"
        INSERT INTO jobby_jobs (
            id,
            job_name,
            job_param,
            status,
            created_at,
            scheduled_start_at,
            next_job_id
        )
        VALUES (
            $1,
            $2,
            $3,
            $4,
            $5,
            $6,
            $7
        )
    ";

    public static async Task ExecuteAsync(NpgsqlConnection conn, IReadOnlyList<Job> jobs)
    {
        await using var batch = new NpgsqlBatch(conn);
        PrepareCommand(batch, jobs);
        await batch.ExecuteNonQueryAsync();
    }

    public static void Execute(NpgsqlConnection conn, IReadOnlyList<Job> jobs)
    {
        using var batch = new NpgsqlBatch(conn);
        PrepareCommand(batch, jobs);
        batch.ExecuteNonQuery();
    }

    private static void PrepareCommand(NpgsqlBatch batch, IReadOnlyList<Job> jobs)
    {
        foreach (var job in jobs)
        {
            var cmd = new NpgsqlBatchCommand(CommandText)
            {
                Parameters =
                {
                    new() { Value = job.Id },
                    new() { Value = job.JobName },
                    new() { Value = (object?)job.JobParam ?? DBNull.Value },
                    new() { Value = (int)job.Status },
                    new() { Value = job.CreatedAt },
                    new() { Value = job.ScheduledStartAt },
                    new() { Value = (object?)job.NextJobId ?? DBNull.Value },
                }
            };
            batch.BatchCommands.Add(cmd);
        }
    }
}
