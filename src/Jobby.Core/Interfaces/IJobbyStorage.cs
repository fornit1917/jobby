using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobbyStorage
{
    Task InsertJobAsync(JobCreationModel job);
    Task BulkInsertJobsAsync(IReadOnlyList<JobCreationModel> jobs);
    void InsertJob(JobCreationModel job);
    void BulkInsertJobs(IReadOnlyList<JobCreationModel> jobs);
    
    Task TakeBatchToProcessingAsync(string serverId, int maxBatchSize, List<JobExecutionModel> result);
    
    Task UpdateProcessingJobToFailedAsync(Guid jobId, string error);

    Task RescheduleProcessingJobAsync(Guid jobId, DateTime sheduledStartTime, string? error = null);

    Task UpdateProcessingJobToCompletedAsync(Guid jobId, Guid? nextJobId = null);
    Task BulkUpdateProcessingJobsToCompletedAsync(IReadOnlyList<Guid> jobIds, IReadOnlyList<Guid> nextJobIds);

    Task DeleteProcessingJobAsync(Guid jobId, Guid? nextJobId = null);
    Task BulkDeleteProcessingJobsAsync(IReadOnlyList<Guid> jobIds, IReadOnlyList<Guid>? nextJobIds = null);
    void BulkDeleteProcessingJobs(IReadOnlyList<Guid> jobIds);

    Task BulkDeleteNotStartedJobsAsync(IReadOnlyList<Guid> jobIds);
    void BulkDeleteNotStartedJobs(IReadOnlyList<Guid> jobIds);

    Task DeleteRecurrentAsync(string jobName);
    void DeleteRecurrent(string jobName);

    Task SendHeartbeatAsync(string serverId);
    Task DeleteLostServersAndRestartTheirJobsAsync(DateTime minLastHeartbeat,
        List<string> deletedServerIds, List<StuckJobModel> stuckJobs);
}
