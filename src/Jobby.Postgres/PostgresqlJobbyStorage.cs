using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Postgres.Commands;
using Npgsql;

namespace Jobby.Postgres;

internal class PostgresqlJobbyStorage : IJobbyStorage
{
    private readonly NpgsqlDataSource _dataSource;

    private readonly BulkCompleteJobsCommand _bulkCompleteJobsCommand;
    private readonly BulkDeleteJobsCommand _bulkDeleteJobsCommand;
    private readonly BulkInsertJobsCommand _bulkInsertJobsCommand;
    private readonly DeleteJobCommand _deleteJobCommand;
    private readonly InsertJobCommand _insertJobCommand;
    private readonly RescheduleCommand _rescheduleJobCommand;
    private readonly TakeBatchToProcessingCommand _takeBatchToProcessingCommand;
    private readonly UpdateStatusCommand _updateStatusCommand;
    private readonly SendHeartbeatCommand _sendHeartbeatCommand;
    private readonly FindAndDeleteLostServersCommands _findAndDeleteLostServersCommand;
    private readonly FindAndRestartStuckJobsCommand _findAndRestartStuckJobsCommand;
    private readonly DeleteRecurrentJobByNameCommand _deleteRecurrentJobByNameCommand;

    public PostgresqlJobbyStorage(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _bulkCompleteJobsCommand = new BulkCompleteJobsCommand(dataSource, settings);
        _bulkDeleteJobsCommand = new BulkDeleteJobsCommand(dataSource, settings);
        _bulkInsertJobsCommand = new BulkInsertJobsCommand(dataSource, settings);
        _deleteJobCommand = new DeleteJobCommand(dataSource, settings);
        _insertJobCommand = new InsertJobCommand(dataSource, settings);
        _rescheduleJobCommand = new RescheduleCommand(dataSource, settings);
        _takeBatchToProcessingCommand = new TakeBatchToProcessingCommand(dataSource, settings);
        _updateStatusCommand = new UpdateStatusCommand(dataSource, settings);
        _sendHeartbeatCommand = new SendHeartbeatCommand(dataSource, settings);
        _findAndDeleteLostServersCommand = new FindAndDeleteLostServersCommands(settings);
        _findAndRestartStuckJobsCommand = new FindAndRestartStuckJobsCommand(settings);
        _deleteRecurrentJobByNameCommand = new DeleteRecurrentJobByNameCommand(dataSource, settings);
    }

    public Task InsertAsync(JobCreationModel job)
    {
        return _insertJobCommand.ExecuteAsync(job);
    }

    public void Insert(JobCreationModel job)
    {
        _insertJobCommand.Execute(job);
    }

    public Task TakeBatchToProcessingAsync(string serverId, int maxBatchSize, List<JobExecutionModel> result)
    {
        return _takeBatchToProcessingCommand.ExecuteAndWriteToListAsync(serverId, DateTime.UtcNow, maxBatchSize, result);
    }

    public Task MarkCompletedAsync(Guid jobId, Guid? nextJobId = null)
    {
        return UpdateStatus(jobId, JobStatus.Completed, error: null, nextJobId);
    }

    public Task MarkFailedAsync(Guid jobId, string error)
    {
        return UpdateStatus(jobId, JobStatus.Failed, error);
    }

    public Task RescheduleAsync(Guid jobId, DateTime sheduledStartTime, string? error = null)
    {
        return _rescheduleJobCommand.ExecuteAsync(jobId, sheduledStartTime, error);
    }

    private Task UpdateStatus(Guid jobId, JobStatus newStatus, string? error = null, Guid? nextJobId = null)
    {
        var finishedAt = DateTime.UtcNow;
        return _updateStatusCommand.ExecuteAsync(jobId, newStatus, error, nextJobId);
    }

    public Task DeleteAsync(Guid jobId, Guid? nextJobId = null)
    {
        return _deleteJobCommand.ExecuteAsync(jobId, nextJobId);
    }

    public Task BulkInsertAsync(IReadOnlyList<JobCreationModel> jobs)
    {
        return _bulkInsertJobsCommand.ExecuteAsync(jobs);
    }

    public void BulkInsert(IReadOnlyList<JobCreationModel> jobs)
    {
        _bulkInsertJobsCommand.Execute(jobs);
    }

    public Task BulkDeleteAsync(IReadOnlyList<Guid> jobIds, IReadOnlyList<Guid>? nextJobIds = null)
    {
        return _bulkDeleteJobsCommand.ExecuteAsync(jobIds, nextJobIds);
    }

    public void BulkDelete(IReadOnlyList<Guid> jobIds)
    {
        _bulkDeleteJobsCommand.Execute(jobIds);
    }

    public Task BulkMarkCompletedAsync(IReadOnlyList<Guid> jobIds, IReadOnlyList<Guid>? nextJobIds = null)
    {
        return _bulkCompleteJobsCommand.ExecuteAsync(jobIds, nextJobIds);
    }

    public Task SendHeartbeatAsync(string serverId)
    {
        return _sendHeartbeatCommand.ExecuteAsync(serverId, DateTime.UtcNow);
    }

    public async Task DeleteLostServersAndRestartTheirJobsAsync(DateTime minLastHeartbeat,
        List<string> deletedServerIds, List<StuckJobModel> stuckJobs)
    {
        deletedServerIds.Clear();
        stuckJobs.Clear();

        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var transaction = await conn.BeginTransactionAsync();
        
        await _findAndDeleteLostServersCommand.ExecuteInTransactionAsync(conn, transaction, minLastHeartbeat, deletedServerIds);
        if (deletedServerIds.Count > 0)
        {
            await _findAndRestartStuckJobsCommand.ExecuteInTransactionAsync(conn, transaction, deletedServerIds, stuckJobs);
        }

        await transaction.CommitAsync();
    }

    public Task DeleteRecurrentAsync(string jobName)
    {
        return _deleteRecurrentJobByNameCommand.ExecuteAsync(jobName);
    }

    public void DeleteRecurrent(string jobName)
    {
        _deleteRecurrentJobByNameCommand.Execute(jobName);
    }
}
