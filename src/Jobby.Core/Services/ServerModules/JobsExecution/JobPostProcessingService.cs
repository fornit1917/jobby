using System.Collections.Concurrent;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Interfaces.ServerModules.JobsExecution;
using Jobby.Core.Models;
using Jobby.Core.Services.Schedulers;
using Microsoft.Extensions.Logging;

namespace Jobby.Core.Services.ServerModules.JobsExecution;

internal class JobPostProcessingService : IJobPostProcessingService
{
    private readonly IJobbyStorage _storage;
    private readonly IJobCompletionService _jobCompletingService;
    private readonly IReadOnlyDictionary<string, ISchedule> _schedulersByType;
    private readonly ILogger<JobPostProcessingService> _logger;

    private readonly record struct RetryQueueItem(JobExecutionModel Job, RetryPolicy? RetryPolicy = null, string? Error = null);
    private readonly ConcurrentQueue<RetryQueueItem> _retryQueue;

    public JobPostProcessingService(IJobbyStorage storage,
        IJobCompletionService jobCompletingService,
        IReadOnlyDictionary<string, ISchedule> schedulersByType,
        ILogger<JobPostProcessingService> logger)
    {
        _storage = storage;
        _jobCompletingService = jobCompletingService;
        _schedulersByType = schedulersByType;
        _logger = logger;
        _retryQueue = new ConcurrentQueue<RetryQueueItem>();
    }

    public bool IsRetryQueueEmpty => _retryQueue.IsEmpty;

    public async Task HandleCompleted(JobExecutionModel job)
    {
        try
        {
            await HandleCompletedInternal(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing executed job, jobName = {JobName}, id = {JobId}", job.JobName, job.Id);
            _retryQueue.Enqueue(new RetryQueueItem(job));
        }
    }

    public async Task HandleFailed(JobExecutionModel job, RetryPolicy retryPolicy, string error)
    {
        try
        {
            await HandleFailedInternal(job, retryPolicy, error);
        }
        catch (Exception statusEx)
        {
            _logger.LogError(statusEx,
                "Error while change status of failed job, jobName = {JobName}, id = {JobId}", job.JobName, job.Id);

            _retryQueue.Enqueue(new RetryQueueItem(job, retryPolicy, error));
        }
    }

    public async Task RescheduleRecurrent(JobExecutionModel job, string? error = null)
    {
        try
        {
            await RescheduleRecurrentInternal(job, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error while reschedule next run for recurrent job, jobName = {JobName}, id = {JobId}", job.JobName, job.Id);

            _retryQueue.Enqueue(new RetryQueueItem(job, null, error));
        }
    }

    public async Task DoRetriesFromQueue(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _retryQueue.TryPeek(out var queueItem))
        {
            if (queueItem.Job.Schedule != null)
            {
                await RescheduleRecurrentInternal(queueItem.Job, queueItem.Error);
            }
            else if (queueItem.RetryPolicy == null)
            {
                await HandleCompletedInternal(queueItem.Job);
            }
            else
            {
                await HandleFailedInternal(queueItem.Job, queueItem.RetryPolicy, queueItem.Error ?? "");
            }

            _logger.LogInformation("Post processing for job successfully retried, jobName = {JobName}, id = {JobId}", queueItem.Job.JobName, queueItem.Job.Id);
            _retryQueue.TryDequeue(out queueItem);
        }
    }

    private Task HandleCompletedInternal(JobExecutionModel job)
    {
        return _jobCompletingService.CompleteJob(job);
    }

    private Task HandleFailedInternal(JobExecutionModel job, RetryPolicy retryPolicy, string error)
    {
        TimeSpan? retryInterval = retryPolicy.GetIntervalForNextAttempt(job);
        if (retryInterval.HasValue)
        {
            var scheduledStartTime = DateTime.UtcNow.Add(retryInterval.Value);
            return _storage.RescheduleProcessingJobAsync(job, scheduledStartTime, error);
        }
        else
        {
            return _storage.UpdateProcessingJobToFailedAsync(job, error);
        }
    }

    private Task RescheduleRecurrentInternal(JobExecutionModel job, string? error)
    {
        ArgumentNullException.ThrowIfNull(job.Schedule, nameof(job.Schedule));

        DateTime nextStartAt;
        var schedulerType = job.SchedulerType ?? JobbySchedulerTypes.CronFromNow;
        if (!_schedulersByType.TryGetValue(schedulerType, out var scheduler))
        {
            nextStartAt = DateTime.UtcNow.Add(TimeSpan.FromMinutes(1));
            _logger.LogError("Recurrent job {JobName} with id={JobId} has invalid SchedulerType: {SchedulerType}", 
                job.JobName, job.Id, schedulerType);
        }
        else
        {
            nextStartAt = scheduler.GetNextStartTime(job.Schedule, job.ScheduledStartAt); 
        }
        
        return _storage.RescheduleProcessingJobAsync(job, nextStartAt, error);
    }
}
