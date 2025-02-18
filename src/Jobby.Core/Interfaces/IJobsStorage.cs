using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobsStorage
{
    Task<long> InsertAsync(JobModel job);
    long Insert(JobModel job);
    Task<JobModel?> TakeToProcessingAsync();
    Task TakeBatchToProcessingAsync(int maxBatchSize, List<JobModel> result);
    Task MarkCompletedAsync(long jobId);
    Task MarkFailedAsync(long jobId);
    Task RescheduleAsync(long jobId, DateTime sheduledStartTime);
}
