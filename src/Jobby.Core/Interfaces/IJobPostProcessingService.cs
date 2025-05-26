using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

internal interface IJobPostProcessingService : IDisposable
{
    Task HandleCompleted(JobExecutionModel job);
    Task HandleFailed(JobExecutionModel job, RetryPolicy retryPolicy);
    Task RescheduleRecurrent(JobExecutionModel job);

    bool IsRetryQueueEmpty {  get; }
    Task DoRetriesFromQueue();
}
