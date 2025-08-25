using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobbyStorage
{
    Task InsertJobAsync(JobCreationModel job);
    Task BulkInsertJobsAsync(IReadOnlyList<JobCreationModel> jobs);
    void InsertJob(JobCreationModel job);
    void BulkInsertJobs(IReadOnlyList<JobCreationModel> jobs);
    
    Task TakeBatchToProcessingAsync(string serverId, int maxBatchSize, List<JobExecutionModel> result);
    
    Task UpdateProcessingJobToFailedAsync(ProcessingJob job, string error);
    Task RescheduleProcessingJobAsync(ProcessingJob job, DateTime sheduledStartTime, string? error = null);
    Task UpdateProcessingJobToCompletedAsync(ProcessingJob job, Guid? nextJobId = null);
    Task BulkUpdateProcessingJobsToCompletedAsync(ProcessingJobsList jobs, IReadOnlyList<Guid> nextJobIds);
    Task DeleteProcessingJobAsync(ProcessingJob job, Guid? nextJobId = null);
    Task BulkDeleteProcessingJobsAsync(ProcessingJobsList jobs, IReadOnlyList<Guid>? nextJobIds = null);

    Task BulkDeleteNotStartedJobsAsync(IReadOnlyList<Guid> jobIds);
    void BulkDeleteNotStartedJobs(IReadOnlyList<Guid> jobIds);

    Task DeleteRecurrentAsync(string jobName);
    void DeleteRecurrent(string jobName);

    Task SendHeartbeatAsync(string serverId);
    Task DeleteLostServersAndRestartTheirJobsAsync(DateTime minLastHeartbeat,
        List<string> deletedServerIds, List<StuckJobModel> stuckJobs);
}
