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
            @id,
            @job_name,
            @job_param,
            @status,
            @created_at,
            @scheduled_start_at,
            @next_job_id
        )
    ";

    public static async Task ExecuteAsync(NpgsqlConnection conn, IReadOnlyList<Job> jobs)
    {
        await using var batch = new NpgsqlBatch(conn);
        foreach (var job in jobs)
        {
            var cmd = new NpgsqlBatchCommand(CommandText)
            {
                Parameters =
                {
                    new("id", job.Id),
                    new("job_name", job.JobName),
                    new("job_param", (object?)job.JobParam ?? DBNull.Value),
                    new("status", (int)job.Status),
                    new("created_at", job.CreatedAt),
                    new("scheduled_start_at", job.ScheduledStartAt),
                    new("next_job_id", (object?)job.NextJobId ?? DBNull.Value),
                }
            };
            batch.BatchCommands.Add(cmd);
        }
        await batch.ExecuteNonQueryAsync();
    }

    public static void Execute(NpgsqlConnection conn, IReadOnlyList<Job> jobs)
    {
        using var batch = new NpgsqlBatch(conn);
        foreach (var job in jobs)
        {
            var cmd = new NpgsqlBatchCommand(CommandText)
            {
                Parameters =
                {
                    new("id", job.Id),
                    new("job_name", job.JobName),
                    new("job_param", (object?)job.JobParam ?? DBNull.Value),
                    new("status", (int)job.Status),
                    new("created_at", job.CreatedAt),
                    new("scheduled_start_at", job.ScheduledStartAt),
                    new("next_job_id", (object?)job.NextJobId ?? DBNull.Value),
                }
            };
            batch.BatchCommands.Add(cmd);
        }
        batch.ExecuteNonQuery();
    }
}
