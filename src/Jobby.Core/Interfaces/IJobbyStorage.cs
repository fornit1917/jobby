using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobbyStorage
{
    Task<Guid> InsertJobAsync(JobCreationModel job);
    Task BulkInsertJobsAsync(IReadOnlyList<JobCreationModel> jobs);
    Guid InsertJob(JobCreationModel job);
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
    
    Task BulkDeleteRecurrentAsync(IReadOnlyList<Guid> jobIds);
    void BulkDeleteRecurrent(IReadOnlyList<Guid> jobIds);

    Task DeleteExclusiveByNameAsync(string jobName);
    void DeleteExclusiveByName(string jobName);

    Task SendHeartbeatAsync(string serverId);

    [Obsolete("This overload computes minLastHeartbeat from the application clock, " +
              "which leads to false positive lost server detection when the application " +
              "and the database clocks are out of sync. Use the TimeSpan overload instead: " +
              "it computes the cutoff time on the database side.")]
    Task DeleteLostServersAndRestartTheirJobsAsync(DateTime minLastHeartbeat,
        List<string> deletedServerIds, List<StuckJobModel> stuckJobs);

    /// <summary>
    /// Deletes servers whose last heartbeat is older than <paramref name="maxNoHeartbeatInterval"/>,
    /// with the cutoff time computed on the database side (now() - interval),
    /// and restarts their stuck jobs.
    /// </summary>
    Task DeleteLostServersAndRestartTheirJobsAsync(TimeSpan maxNoHeartbeatInterval,
        List<string> deletedServerIds, List<StuckJobModel> stuckJobs)
    {
#pragma warning disable CS0618 // Call the legacy overload for backward compatibility with existing implementations
        return DeleteLostServersAndRestartTheirJobsAsync(DateTime.UtcNow - maxNoHeartbeatInterval,
            deletedServerIds, stuckJobs);
#pragma warning restore CS0618
    }
}
