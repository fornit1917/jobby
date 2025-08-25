using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Postgres.Commands;
using Npgsql;

namespace Jobby.Postgres;

internal class PostgresqlJobbyStorage : IJobbyStorage
{
    private readonly NpgsqlDataSource _dataSource;

    private readonly BulkCompleteProcessingJobsCommand _bulkCompleteProcessingJobsCommand;
    private readonly BulkDeleteProcessingJobsCommand _bulkDeleteProcessingJobsCommand;
    private readonly BulkInsertJobsCommand _bulkInsertJobsCommand;
    private readonly DeleteProcessingJobCommand _deleteProcessingJobCommand;
    private readonly InsertJobCommand _insertJobCommand;
    private readonly RescheduleProcessingJobCommand _rescheduleProcessingJobCommand;
    private readonly TakeBatchToProcessingCommand _takeBatchToProcessingCommand;
    private readonly UpdateFromProcessingStatusCommand _updateFromProcessingStatusCommand;
    private readonly SendHeartbeatCommand _sendHeartbeatCommand;
    private readonly FindAndDeleteLostServersCommands _findAndDeleteLostServersCommand;
    private readonly FindAndRestartStuckJobsCommand _findAndRestartStuckJobsCommand;
    private readonly DeleteRecurrentJobByNameCommand _deleteRecurrentJobByNameCommand;
    private readonly BulkDeleteNotStartedJobsCommand _bulkDeleteNotStartedJobsCommand;

    public PostgresqlJobbyStorage(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _bulkCompleteProcessingJobsCommand = new BulkCompleteProcessingJobsCommand(dataSource, settings);
        _bulkDeleteProcessingJobsCommand = new BulkDeleteProcessingJobsCommand(dataSource, settings);
        _bulkInsertJobsCommand = new BulkInsertJobsCommand(dataSource, settings);
        _deleteProcessingJobCommand = new DeleteProcessingJobCommand(dataSource, settings);
        _insertJobCommand = new InsertJobCommand(dataSource, settings);
        _rescheduleProcessingJobCommand = new RescheduleProcessingJobCommand(dataSource, settings);
        _takeBatchToProcessingCommand = new TakeBatchToProcessingCommand(dataSource, settings);
        _updateFromProcessingStatusCommand = new UpdateFromProcessingStatusCommand(dataSource, settings);
        _sendHeartbeatCommand = new SendHeartbeatCommand(dataSource, settings);
        _findAndDeleteLostServersCommand = new FindAndDeleteLostServersCommands(settings);
        _findAndRestartStuckJobsCommand = new FindAndRestartStuckJobsCommand(settings);
        _deleteRecurrentJobByNameCommand = new DeleteRecurrentJobByNameCommand(dataSource, settings);
        _bulkDeleteNotStartedJobsCommand = new BulkDeleteNotStartedJobsCommand(dataSource, settings);
    }

    public Task InsertJobAsync(JobCreationModel job)
    {
        return _insertJobCommand.ExecuteAsync(job);
    }

    public void InsertJob(JobCreationModel job)
    {
        _insertJobCommand.Execute(job);
    }

    public Task TakeBatchToProcessingAsync(string serverId, int maxBatchSize, List<JobExecutionModel> result)
    {
        return _takeBatchToProcessingCommand.ExecuteAndWriteToListAsync(serverId, DateTime.UtcNow, maxBatchSize, result);
    }

    public Task UpdateProcessingJobToCompletedAsync(ProcessingJob job, Guid? nextJobId = null)
    {
        return UpdateFromProcessingStatus(job, JobStatus.Completed, error: null, nextJobId);
    }

    public Task UpdateProcessingJobToFailedAsync(ProcessingJob job, string error)
    {
        return UpdateFromProcessingStatus(job, JobStatus.Failed, error);
    }

    public Task RescheduleProcessingJobAsync(ProcessingJob job, DateTime sheduledStartTime, string? error = null)
    {
        return _rescheduleProcessingJobCommand.ExecuteAsync(job, sheduledStartTime, error);
    }

    private Task UpdateFromProcessingStatus(ProcessingJob job, JobStatus newStatus, string? error = null, Guid? nextJobId = null)
    {
        return _updateFromProcessingStatusCommand.ExecuteAsync(job, newStatus, error, nextJobId);
    }

    public Task DeleteProcessingJobAsync(ProcessingJob job, Guid? nextJobId = null)
    {
        return _deleteProcessingJobCommand.ExecuteAsync(job, nextJobId);
    }

    public Task BulkInsertJobsAsync(IReadOnlyList<JobCreationModel> jobs)
    {
        return _bulkInsertJobsCommand.ExecuteAsync(jobs);
    }

    public void BulkInsertJobs(IReadOnlyList<JobCreationModel> jobs)
    {
        _bulkInsertJobsCommand.Execute(jobs);
    }

    public Task BulkDeleteProcessingJobsAsync(ProcessingJobsList jobs, IReadOnlyList<Guid>? nextJobIds = null)
    {
        return _bulkDeleteProcessingJobsCommand.ExecuteAsync(jobs, nextJobIds);
    }

    public Task BulkUpdateProcessingJobsToCompletedAsync(ProcessingJobsList jobs, IReadOnlyList<Guid> nextJobIds)
    {
        return _bulkCompleteProcessingJobsCommand.ExecuteAsync(jobs, nextJobIds);
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

    public Task BulkDeleteNotStartedJobsAsync(IReadOnlyList<Guid> jobIds)
    {
        return _bulkDeleteNotStartedJobsCommand.ExecuteAsync(jobIds);
    }

    public void BulkDeleteNotStartedJobs(IReadOnlyList<Guid> jobIds)
    {
        _bulkDeleteNotStartedJobsCommand.Execute(jobIds);
    }
}
