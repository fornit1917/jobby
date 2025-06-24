using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobbyStorage
{
    Task InsertAsync(JobCreationModel job);
    Task BulkInsertAsync(IReadOnlyList<JobCreationModel> jobs);
    void Insert(JobCreationModel job);
    void BulkInsert(IReadOnlyList<JobCreationModel> jobs);
    
    Task TakeBatchToProcessingAsync(string serverId, int maxBatchSize, List<JobExecutionModel> result);
    
    Task MarkFailedAsync(Guid jobId, string error);
    Task RescheduleAsync(Guid jobId, DateTime sheduledStartTime, string? error = null);

    Task MarkCompletedAsync(Guid jobId, Guid? nextJobId = null);
    Task BulkMarkCompletedAsync(IReadOnlyList<Guid> jobIds, IReadOnlyList<Guid> nextJobIds);

    Task DeleteAsync(Guid jobId, Guid? nextJobId = null);
    Task BulkDeleteAsync(IReadOnlyList<Guid> jobIds, IReadOnlyList<Guid>? nextJobIds = null);
    void BulkDelete(IReadOnlyList<Guid> jobIds);

    Task DeleteRecurrentAsync(string jobName);
    void DeleteRecurrent(string jobName);

    Task SendHeartbeatAsync(string serverId);
    Task DeleteLostServersAndRestartTheirJobsAsync(DateTime minLastHeartbeat,
        List<string> deletedServerIds, List<StuckJobModel> stuckJobs);
}
