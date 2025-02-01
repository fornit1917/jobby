using Jobby.Abstractions.Models;

namespace Jobby.Abstractions.CommonServices;

public interface IJobsStorage
{
    Task<long> InsertAsync(JobModel job);
    Task<JobModel?> TakeToProcessingAsync();
    Task MarkCompletedAsync(long jobId);
    Task MarkFailedAsync(long jobId);
    Task RescheduleAsync(long jobId, DateTime sheduledStartTime);
}
