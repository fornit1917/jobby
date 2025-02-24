using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Postgres.Commands;
using Npgsql;

namespace Jobby.Postgres;

public class PgJobsStorage : IJobsStorage
{
    private readonly NpgsqlDataSource _dataSource;

    public PgJobsStorage(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<long> InsertAsync(Job job)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        var id = await InsertJobCommand.ExecuteAndGetIdAsync(conn, job);
        return id;
    }

    public long Insert(Job job)
    {
        using var conn = _dataSource.OpenConnection();
        var id = InsertJobCommand.ExecuteAndGetId(conn, job);
        return id;
    }

    public async Task<Job?> TakeToProcessingAsync()
    {
        // todo: maybe it will be better to return some other model from this method
        var now = DateTime.UtcNow;
        await using var conn = await _dataSource.OpenConnectionAsync();
        var job = await TakeToProcessingCommand.ExecuteAsync(conn, now);
        return job;
    }

    public async Task TakeBatchToProcessingAsync(int maxBatchSize, List<Job> result)
    {
        // todo: maybe it will be better to return some other model from this method
        var now = DateTime.UtcNow;
        await using var conn = await _dataSource.OpenConnectionAsync();
        await TakeBatchToProcessingCommand.ExecuteAndWriteToListAsync(conn, now, maxBatchSize, result);
    }

    public Task MarkCompletedAsync(long jobId)
    {
        return UpdateStatus(jobId, JobStatus.Completed);
    }

    public Task MarkFailedAsync(long jobId)
    {
        // todo: write error message to job
        return UpdateStatus(jobId, JobStatus.Failed);
    }

    public async Task RescheduleAsync(long jobId, DateTime sheduledStartTime)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await RescheduleCommand.ExecuteAsync(conn, jobId, sheduledStartTime);
    }

    private async Task UpdateStatus(long jobId, JobStatus newStatus)
    {
        var finishedAt = DateTime.UtcNow;
        await using var conn = await _dataSource.OpenConnectionAsync();
        await UpdateStatusCommand.ExecuteAsync(conn, jobId, newStatus);
    }

    public async Task DeleteAsync(long jobId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await DeleteJobCommand.ExecuteAsync(conn, jobId);
    }
}
