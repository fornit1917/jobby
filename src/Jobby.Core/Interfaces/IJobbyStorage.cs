using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobbyStorage
{
    Task InsertAsync(Job job);
    Task BulkInsertAsync(IReadOnlyList<Job> jobs);

    void Insert(Job job);
    void BulkInsert(IReadOnlyList<Job> jobs);
    
    Task TakeBatchToProcessingAsync(int maxBatchSize, List<Job> result);
    
    Task MarkFailedAsync(Guid jobId);
    Task RescheduleAsync(Guid jobId, DateTime sheduledStartTime);

    Task MarkCompletedAsync(Guid jobId, Guid? nextJobId = null);
    Task BulkMarkCompletedAsync(IReadOnlyList<Guid> jobIds, IReadOnlyList<Guid>? nextJobIds = null);

    Task DeleteAsync(Guid jobId, Guid? nextJobId = null);
    Task BulkDeleteAsync(IReadOnlyList<Guid> jobIds, IReadOnlyList<Guid>? nextJobIds = null);
}
