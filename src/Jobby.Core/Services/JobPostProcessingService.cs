using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Jobby.Core.Services;

internal class JobPostProcessingService : IJobPostProcessingService
{
    private readonly IJobbyStorage _storage;
    private readonly IJobCompletionService _jobCompletingService;
    private readonly ILogger<JobPostProcessingService> _logger;
    private readonly JobbyServerSettings _settings;

    private readonly record struct RetryQueueItem(Job Job, RetryPolicy? RetryPolicy = null);
    private readonly ConcurrentQueue<RetryQueueItem> _retryQueue;

    public JobPostProcessingService(IJobbyStorage storage,
        IJobCompletionService jobCompletingService,
        ILogger<JobPostProcessingService> logger,
        JobbyServerSettings settings)
    {
        _storage = storage;
        _logger = logger;
        _settings = settings;

        _retryQueue = new ConcurrentQueue<RetryQueueItem>();
        _jobCompletingService = jobCompletingService;
    }

    public bool IsRetryQueueEmpty => _retryQueue.IsEmpty;

    public async Task HandleCompleted(Job job)
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

    public async Task HandleFailed(Job job, RetryPolicy retryPolicy)
    {
        try
        {
            await HandleFailedInternal(job, retryPolicy);
        }
        catch (Exception statusEx)
        {
            _logger.LogError(statusEx,
                "Error while change status of failed job, jobName = {JobName}, id = {JobId}", job.JobName, job.Id);

            _retryQueue.Enqueue(new RetryQueueItem(job, retryPolicy));
        }
    }

    public async Task RescheduleRecurrent(Job job)
    {
        try
        {
            await RescheduleRecurrentInternal(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error while reschedule next run for recurrent job, jobName = {JobName}, id = {JobId}", job.JobName, job.Id);

            _retryQueue.Enqueue(new RetryQueueItem(job));
        }
    }

    public async Task DoRetriesFromQueue()
    {
        while (_retryQueue.TryPeek(out var queueItem))
        {
            if (queueItem.Job.Cron != null)
            {
                await RescheduleRecurrentInternal(queueItem.Job);
            }
            else if (queueItem.RetryPolicy == null)
            {
                await HandleCompletedInternal(queueItem.Job);
            }
            else
            {
                await HandleFailedInternal(queueItem.Job, queueItem.RetryPolicy);
            }

            _logger.LogInformation("Post processing for job successfully retried, jobName = {JobName}, id = {JobId}", queueItem.Job.JobName, queueItem.Job.Id);
            _retryQueue.TryDequeue(out queueItem);
        }
    }

    private Task HandleCompletedInternal(Job job)
    {
        return _jobCompletingService.CompleteJob(job.Id, job.NextJobId);
    }

    private Task HandleFailedInternal(Job job, RetryPolicy retryPolicy)
    {
        TimeSpan? retryInterval = retryPolicy.GetIntervalForNextAttempt(job);
        if (retryInterval.HasValue)
        {
            var sheduledStartTime = DateTime.UtcNow.Add(retryInterval.Value);
            return _storage.RescheduleAsync(job.Id, sheduledStartTime);
        }
        else
        {
            return _storage.MarkFailedAsync(job.Id);
        }
    }

    private Task RescheduleRecurrentInternal(Job job)
    {
        ArgumentNullException.ThrowIfNull(job.Cron, nameof(job.Cron));
        var nextStartAt = CronHelper.GetNext(job.Cron, DateTime.UtcNow);
        return _storage.RescheduleAsync(job.Id, nextStartAt);
    }

    public void Dispose()
    {
        if (_jobCompletingService is IDisposable disposableJobCompletionService)
        {
            disposableJobCompletionService.Dispose();
        }
    }
}
