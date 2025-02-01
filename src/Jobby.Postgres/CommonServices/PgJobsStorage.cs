using Jobby.Abstractions.CommonServices;
using Jobby.Abstractions.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.CommonServices;

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

    private const string UpdateStatusSql = @"
        UPDATE jobby_jobs
        SET
            status = @status,
            last_finished_at = @last_finished_at
        WHERE id = @id;
    ";

    private const string UpdateStatusAndSheduledStartTimeSql = @"
        UPDATE jobby_jobs
        SET
            status = @status,
            last_finished_at = @last_finished_at,
            scheduled_start_at = @scheduled_start_at
        WHERE id = @id;
    ";

    private readonly string TakeToProcessingSql = $@"
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

    public async Task<JobModel?> TakeToProcessingAsync()
    {
        // todo: maybe it will be better to return some other model from this method

        var now = DateTime.UtcNow;
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(TakeToProcessingSql, conn)
        {
            Parameters =
            {
                new("now", now),
            }
        };

        await using var reader = await cmd.ExecuteReaderAsync();
        var job = await reader.GetJobAsync();
        return job;
    }

    public Task MarkCompletedAsync(long jobId)
    {
        return UpdateStatusAndFinishedTime(jobId, JobStatus.Completed);
    }

    public Task MarkFailedAsync(long jobId)
    {
        // todo: write error message to job
        return UpdateStatusAndFinishedTime(jobId, JobStatus.Failed);
    }

    public async Task RescheduleAsync(long jobId, DateTime sheduledStartTime)
    {
        var finishedAt = DateTime.UtcNow;
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(UpdateStatusAndSheduledStartTimeSql, conn)
        {
            Parameters =
            {
                new("status", (int)JobStatus.Scheduled),
                new("last_finished_at", finishedAt),
                new("id", jobId),

            }
        };
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task UpdateStatusAndFinishedTime(long jobId, JobStatus jobStatus)
    {
        var finishedAt = DateTime.UtcNow;
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(UpdateStatusSql, conn)
        {
            Parameters =
            {
                new("status", (int)JobStatus.Completed),
                new("last_finished_at", finishedAt),
                new("id", jobId),

            }
        };
        await cmd.ExecuteNonQueryAsync();
    }
}
