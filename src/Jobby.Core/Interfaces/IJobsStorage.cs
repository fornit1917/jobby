using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobsStorage
{
    Task<Guid> InsertAsync(Job job);
    Task BulkInsertAsync(IReadOnlyList<Job> jobs);

    Guid Insert(Job job);
    void BulkInsert(IReadOnlyList<Job> jobs);
    
    Task<Job?> TakeToProcessingAsync();
    Task TakeBatchToProcessingAsync(int maxBatchSize, List<Job> result);
    Task MarkCompletedAsync(Guid jobId, Guid? nextJobId = null);
    Task MarkFailedAsync(Guid jobId);
    Task RescheduleAsync(Guid jobId, DateTime sheduledStartTime);
    Task DeleteAsync(Guid jobId, Guid? nextJobId = null);
}
