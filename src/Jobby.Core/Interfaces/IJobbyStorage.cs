using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobbyStorage
{
    Task InsertAsync(JobCreationModel job);
    Task BulkInsertAsync(IReadOnlyList<JobCreationModel> jobs);
    void Insert(JobCreationModel job);
    void BulkInsert(IReadOnlyList<JobCreationModel> jobs);
    
    Task TakeBatchToProcessingAsync(string serverId, int maxBatchSize, List<JobExecutionModel> result);
    
    Task MarkFailedAsync(Guid jobId);
    Task RescheduleAsync(Guid jobId, DateTime sheduledStartTime);

    Task MarkCompletedAsync(Guid jobId, Guid? nextJobId = null);
    Task BulkMarkCompletedAsync(IReadOnlyList<Guid> jobIds, IReadOnlyList<Guid>? nextJobIds = null);

    Task DeleteAsync(Guid jobId, Guid? nextJobId = null);
    Task BulkDeleteAsync(IReadOnlyList<Guid> jobIds, IReadOnlyList<Guid>? nextJobIds = null);

    Task SendHeartbeatAsync(string serverId);
}
