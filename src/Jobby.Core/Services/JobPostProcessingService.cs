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

    private readonly record struct RetryQueueItem(JobExecutionModel Job, RetryPolicy? RetryPolicy = null, string? Error = null);
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

    public async Task DoRetriesFromQueue()
    {
        while (_retryQueue.TryPeek(out var queueItem))
        {
            if (queueItem.Job.Cron != null)
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
        return _jobCompletingService.CompleteJob(job.Id, job.NextJobId);
    }

    private Task HandleFailedInternal(JobExecutionModel job, RetryPolicy retryPolicy, string error)
    {
        TimeSpan? retryInterval = retryPolicy.GetIntervalForNextAttempt(job);
        if (retryInterval.HasValue)
        {
            var sheduledStartTime = DateTime.UtcNow.Add(retryInterval.Value);
            return _storage.RescheduleAsync(job.Id, sheduledStartTime, error);
        }
        else
        {
            return _storage.MarkFailedAsync(job.Id, error);
        }
    }

    private Task RescheduleRecurrentInternal(JobExecutionModel job, string? error)
    {
        ArgumentNullException.ThrowIfNull(job.Cron, nameof(job.Cron));
        var nextStartAt = CronHelper.GetNext(job.Cron, DateTime.UtcNow);
        return _storage.RescheduleAsync(job.Id, nextStartAt, error);
    }

    public void Dispose()
    {
        if (_jobCompletingService is IDisposable disposableJobCompletionService)
        {
            disposableJobCompletionService.Dispose();
        }
    }
}
