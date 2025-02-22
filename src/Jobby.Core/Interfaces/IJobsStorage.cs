using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobsStorage
{
    Task<long> InsertAsync(Job job);
    long Insert(Job job);
    Task<Job?> TakeToProcessingAsync();
    Task TakeBatchToProcessingAsync(int maxBatchSize, List<Job> result);
    Task MarkCompletedAsync(long jobId);
    Task MarkFailedAsync(long jobId);
    Task RescheduleAsync(long jobId, DateTime sheduledStartTime);
}
