using Jobby.Core.Models;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal static class InsertJobCommand
{
    // todo: remove parameters which are not actual for new jobs
    // todo: remove returning id
    private const string CommandText = @"
        INSERT INTO jobby_jobs (
            id,
            job_name,
            job_param,
            status,
            created_at,
            scheduled_start_at,
            last_started_at,
            last_finished_at,
            cron,
            next_job_id
        )
        VALUES (
            @id,
            @job_name,
            @job_param,
            @status,
            @created_at,
            @scheduled_start_at,
            @last_started_at,
            @last_finished_at,
            @cron,
            @next_job_id
        )
        ON CONFLICT (job_name) WHERE cron IS NOT null DO 
        UPDATE SET
	        cron = @cron,
	        scheduled_start_at = @scheduled_start_at
        RETURNING id;
    ";

    private static NpgsqlCommand CreateCommand(NpgsqlConnection conn, Job job)
    {
        return new NpgsqlCommand(CommandText, conn)
        {
            Parameters =
            {
                new("id", job.Id),
                new("job_name", job.JobName),
                new("job_param", (object?)job.JobParam ?? DBNull.Value),
                new("status", (int)job.Status),
                new("created_at", job.CreatedAt),
                new("scheduled_start_at", job.ScheduledStartAt),
                new("last_started_at", (object?)job.LastStartedAt ?? DBNull.Value),
                new("last_finished_at", (object?)job.LastFinishedAt ?? DBNull.Value),
                new("cron", (object?)job.Cron ?? DBNull.Value),
                new("next_job_id", (object?)job.NextJobId ?? DBNull.Value),
            }
        };
    }

    public static async Task<Guid> ExecuteAndGetIdAsync(NpgsqlConnection conn, Job job)
    {
        await using var cmd = CreateCommand(conn, job);
        object? id = await cmd.ExecuteScalarAsync();
        return id != null ? (Guid)id : Guid.Empty;
    }

    public static Guid ExecuteAndGetId(NpgsqlConnection conn, Job job)
    {
        using var cmd = CreateCommand(conn, job);
        object? id = cmd.ExecuteScalar();
        return id != null ? (Guid)id : Guid.Empty;
    }
}
