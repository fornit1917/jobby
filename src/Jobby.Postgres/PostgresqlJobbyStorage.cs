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
    private readonly DeleteExclusiveJobByNameCommand _deleteExclusiveJobByNameCommand;
    private readonly BulkDeleteNotStartedJobsCommand _bulkDeleteNotStartedJobsCommand;
    private readonly BulkDeleteRecurrentCommand _bulkDeleteRecurrentCommand;

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
        _deleteExclusiveJobByNameCommand = new DeleteExclusiveJobByNameCommand(dataSource, settings);
        _bulkDeleteNotStartedJobsCommand = new BulkDeleteNotStartedJobsCommand(dataSource, settings);
        _bulkDeleteRecurrentCommand = new BulkDeleteRecurrentCommand(dataSource, settings);
    }

    public Task<Guid> InsertJobAsync(JobCreationModel job)
    {
        return _insertJobCommand.ExecuteAsync(job);
    }

    public Guid InsertJob(JobCreationModel job)
    {
        return _insertJobCommand.Execute(job);
    }

    public Task TakeBatchToProcessingAsync(GetJobsRequest request, List<JobExecutionModel> result)
    {
        return _takeBatchToProcessingCommand.ExecuteAndWriteToListAsync(request, result);
    }

    public Task UpdateProcessingJobToCompletedAsync(JobExecutionModel job)
    {
        return UpdateFromProcessingStatus(job, JobStatus.Completed, error: null);
    }

    public Task UpdateProcessingJobToFailedAsync(JobExecutionModel job, string error)
    {
        return UpdateFromProcessingStatus(job, JobStatus.Failed, error);
    }

    public Task RescheduleProcessingJobAsync(JobExecutionModel job, DateTime sheduledStartTime, string? error = null)
    {
        return _rescheduleProcessingJobCommand.ExecuteAsync(job, sheduledStartTime, error);
    }

    private Task UpdateFromProcessingStatus(JobExecutionModel job, JobStatus newStatus, string? error)
    {
        return _updateFromProcessingStatusCommand.ExecuteAsync(job, newStatus, error);
    }

    public Task DeleteProcessingJobAsync(JobExecutionModel job)
    {
        return _deleteProcessingJobCommand.ExecuteAsync(job);
    }

    public Task BulkInsertJobsAsync(IReadOnlyList<JobCreationModel> jobs)
    {
        return _bulkInsertJobsCommand.ExecuteAsync(jobs);
    }

    public void BulkInsertJobs(IReadOnlyList<JobCreationModel> jobs)
    {
        _bulkInsertJobsCommand.Execute(jobs);
    }

    public Task BulkDeleteProcessingJobsAsync(CompleteJobsBatch jobs)
    {
        return _bulkDeleteProcessingJobsCommand.ExecuteAsync(jobs);
    }

    public Task BulkUpdateProcessingJobsToCompletedAsync(CompleteJobsBatch jobs)
    {
        return _bulkCompleteProcessingJobsCommand.ExecuteAsync(jobs);
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

    public Task DeleteExclusiveByNameAsync(string jobName)
    {
        return _deleteExclusiveJobByNameCommand.ExecuteAsync(jobName);
    }

    public void DeleteExclusiveByName(string jobName)
    {
        _deleteExclusiveJobByNameCommand.Execute(jobName);
    }

    public Task BulkDeleteNotStartedJobsAsync(IReadOnlyList<Guid> jobIds)
    {
        return _bulkDeleteNotStartedJobsCommand.ExecuteAsync(jobIds);
    }

    public void BulkDeleteNotStartedJobs(IReadOnlyList<Guid> jobIds)
    {
        _bulkDeleteNotStartedJobsCommand.Execute(jobIds);
    }

    public Task BulkDeleteRecurrentAsync(IReadOnlyList<Guid> jobIds)
    {
        return _bulkDeleteRecurrentCommand.ExecuteAsync(jobIds);
    }
    
    public void BulkDeleteRecurrent(IReadOnlyList<Guid> jobIds)
    {
        _bulkDeleteRecurrentCommand.Execute(jobIds);
    }
}
