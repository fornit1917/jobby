using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

internal interface IJobPostProcessingService : IDisposable
{
    Task HandleCompleted(Job job);
    Task HandleFailed(Job job, RetryPolicy retryPolicy);
    Task RescheduleRecurrent(Job job);

    bool IsRetryQueueEmpty {  get; }
    Task DoRetriesFromQueue();
}
