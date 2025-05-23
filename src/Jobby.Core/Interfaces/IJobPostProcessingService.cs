using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

internal interface IJobPostProcessingService : IDisposable
{
    Task HandleCompletedAsync(Job job);
    Task HandleFailedAsync(Job job, RetryPolicy retryPolicy);
    Task RescheduleRecurrentAsync(Job job);

    bool IsRetryQueueEmpty {  get; }
    Task DoRetriesFromQueueAsync();
}
