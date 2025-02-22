using Jobby.Core.Models;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal static class InsertJobCommand
{
    private const string CommandText = @"
        INSERT INTO jobby_jobs (
            job_name,
            job_param,
            status,
            created_at,
            scheduled_start_at,
            last_started_at,
            last_finished_at,
            cron
        )
        VALUES (
            @job_name,
            @job_param,
            @status,
            @created_at,
            @scheduled_start_at,
            @last_started_at,
            @last_finished_at,
            @cron
        )
        ON CONFLICT (job_name) WHERE cron IS NOT null DO 
        UPDATE SET
	        cron = @cron,
	        scheduled_start_at = @scheduled_start_at
        RETURNING id;
    ";

    private static NpgsqlCommand CreateCommand(NpgsqlConnection conn, JobModel job)
    {
        return new NpgsqlCommand(CommandText, conn)
        {
            Parameters =
            {
                new("job_name", job.JobName),
                new("job_param", (object?)job.JobParam ?? DBNull.Value),
                new("status", (int)job.Status),
                new("created_at", job.CreatedAt),
                new("scheduled_start_at", job.ScheduledStartAt),
                new("last_started_at", (object?)job.LastStartedAt ?? DBNull.Value),
                new("last_finished_at", (object?)job.LastFinishedAt ?? DBNull.Value),
                new("cron", (object?)job.Cron ?? DBNull.Value),
            }
        };
    }

    public static async Task<long> ExecuteAndGetIdAsync(NpgsqlConnection conn, JobModel job)
    {
        await using var cmd = CreateCommand(conn, job);
        object? id = await cmd.ExecuteScalarAsync();
        return id != null ? (long)id : 0;
    }

    public static long ExecuteAndGetId(NpgsqlConnection conn, JobModel job)
    {
        using var cmd = CreateCommand(conn, job);
        object? id = cmd.ExecuteScalar();
        return id != null ? (long)id : 0;
    }
}
