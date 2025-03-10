using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobsStorage
{
    Task<Guid> InsertAsync(Job job);
    Guid Insert(Job job);
    Task<Job?> TakeToProcessingAsync();
    Task TakeBatchToProcessingAsync(int maxBatchSize, List<Job> result);
    Task MarkCompletedAsync(Guid jobId);
    Task MarkFailedAsync(Guid jobId);
    Task RescheduleAsync(Guid jobId, DateTime sheduledStartTime);
    Task DeleteAsync(Guid jobId);
}
