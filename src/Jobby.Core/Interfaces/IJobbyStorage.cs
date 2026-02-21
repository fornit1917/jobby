using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobbyStorage
{
    Task InsertJobAsync(JobCreationModel job);
    Task BulkInsertJobsAsync(IReadOnlyList<JobCreationModel> jobs);
    void InsertJob(JobCreationModel job);
    void BulkInsertJobs(IReadOnlyList<JobCreationModel> jobs);
    
    Task TakeBatchToProcessingAsync(GetJobsRequest request, List<JobExecutionModel> result);
    
    Task UpdateProcessingJobToFailedAsync(JobExecutionModel job, string error);
    Task RescheduleProcessingJobAsync(JobExecutionModel job, DateTime scheduledStartTime, string? error = null);
    Task UpdateProcessingJobToCompletedAsync(JobExecutionModel job);
    Task BulkUpdateProcessingJobsToCompletedAsync(CompleteJobsBatch jobs);
    Task DeleteProcessingJobAsync(JobExecutionModel job);
    Task BulkDeleteProcessingJobsAsync(CompleteJobsBatch jobs);

    Task BulkDeleteNotStartedJobsAsync(IReadOnlyList<Guid> jobIds);
    void BulkDeleteNotStartedJobs(IReadOnlyList<Guid> jobIds);
    
    Task BulkDeleteJobsAsync(IReadOnlyList<Guid> jobIds);
    void BulkDeleteJobs(IReadOnlyList<Guid> jobIds);

    Task DeleteExclusiveByNameAsync(string jobName);
    void DeleteExclusiveByName(string jobName);

    Task SendHeartbeatAsync(string serverId);
    Task DeleteLostServersAndRestartTheirJobsAsync(DateTime minLastHeartbeat,
        List<string> deletedServerIds, List<StuckJobModel> stuckJobs);
}
