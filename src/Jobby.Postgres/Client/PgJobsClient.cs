using Jobby.Abstractions.CommonServices;
using Jobby.Abstractions.Models;
using Npgsql;

namespace Jobby.Postgres.Client;

public class PgJobsStorage : IJobsStorage
{
    private readonly NpgsqlDataSource _dataSource;

    public PgJobsStorage(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    private const string InsertSql = @"
        INSERT INTO jobby_jobs (
            job_name,
            job_param,
            status,
            created_at,
            scheduled_start_at,
            last_started_at,
            last_finished_at,
            recurrent_job_key,
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
            @recurrent_job_key,
            @cron
        )
        RETURNING id;
    ";

    public async Task<long> InsertAsync(JobModel job)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(InsertSql, conn)
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
                new("recurrent_job_key", (object?)job.RecurrentJobKey ?? DBNull.Value),
                new("cron", (object?)job.Cron ?? DBNull.Value),
            }
        };

        object? id = await cmd.ExecuteScalarAsync();
        return id != null ? (long)id : 0;
    }
}
