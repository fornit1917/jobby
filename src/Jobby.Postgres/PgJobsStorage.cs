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

    public async Task InsertAsync(Job job)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await InsertJobCommand.ExecuteAsync(conn, job);
    }

    public void Insert(Job job)
    {
        using var conn = _dataSource.OpenConnection();
        InsertJobCommand.Execute(conn, job);
    }

    public async Task TakeBatchToProcessingAsync(int maxBatchSize, List<Job> result)
    {
        var now = DateTime.UtcNow;
        await using var conn = await _dataSource.OpenConnectionAsync();
        await TakeBatchToProcessingCommand.ExecuteAndWriteToListAsync(conn, now, maxBatchSize, result);
    }

    public Task MarkCompletedAsync(Guid jobId, Guid? nextJobId = null)
    {
        return UpdateStatus(jobId, JobStatus.Completed, nextJobId);
    }

    public Task MarkFailedAsync(Guid jobId)
    {
        // todo: write error message to job
        return UpdateStatus(jobId, JobStatus.Failed);
    }

    public async Task RescheduleAsync(Guid jobId, DateTime sheduledStartTime)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await RescheduleCommand.ExecuteAsync(conn, jobId, sheduledStartTime);
    }

    private async Task UpdateStatus(Guid jobId, JobStatus newStatus, Guid? nextJobId = null)
    {
        var finishedAt = DateTime.UtcNow;
        await using var conn = await _dataSource.OpenConnectionAsync();
        await UpdateStatusCommand.ExecuteAsync(conn, jobId, newStatus, nextJobId);
    }

    public async Task DeleteAsync(Guid jobId, Guid? nextJobId = null)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await DeleteJobCommand.ExecuteAsync(conn, jobId, nextJobId);
    }

    public async Task BulkInsertAsync(IReadOnlyList<Job> jobs)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await BulkInsertJobsCommand.ExecuteAsync(conn, jobs);
    }

    public void BulkInsert(IReadOnlyList<Job> jobs)
    {
        using var conn = _dataSource.OpenConnection();
        BulkInsertJobsCommand.Execute(conn, jobs);
    }

    public async Task BulkDeleteAsync(IReadOnlyList<Guid> jobIds, IReadOnlyList<Guid>? nextJobIds = null)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await BulkDeleteJobsCommand.ExecuteAsync(conn, jobIds, nextJobIds);
    }
}
