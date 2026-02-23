using Jobby.Core.Models;

namespace Jobby.Core.Interfaces.ServerModules.JobsExecution;

internal interface IJobPostProcessingService
{
    Task HandleCompleted(JobExecutionModel job);
    Task HandleFailed(JobExecutionModel job, RetryPolicy retryPolicy, string error);
    Task RescheduleRecurrent(JobExecutionModel job, string? error = null);

    bool IsRetryQueueEmpty {  get; }
    Task DoRetriesFromQueue(CancellationToken cancellationToken);
}
