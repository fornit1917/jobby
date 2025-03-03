﻿using Jobby.Core.Exceptions;
using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Microsoft.Extensions.Logging;

namespace Jobby.Core.Services;

public class JobbyServer : IJobbyServer
{
    private readonly IJobsStorage _storage;
    private readonly IJobExecutionScopeFactory _scopeFactory;
    private readonly IRetryPolicyService _retryPolicyService;
    private readonly IJobsRegistry _jobsRegistry;
    private readonly IJobParamSerializer _serializer;
    private readonly ILogger<JobbyServer> _logger;

    private readonly JobbyServerSettings _settings;

    private readonly SemaphoreSlim _semaphore;

    private bool _running;

    public JobbyServer(IJobsStorage storage,
        IJobExecutionScopeFactory scopeFactory,
        IRetryPolicyService retryPolicyService,
        IJobsRegistry jobsRegistry,
        IJobParamSerializer serializer,
        ILogger<JobbyServer> logger,
        JobbyServerSettings settings)
    {
        _storage = storage;
        _scopeFactory = scopeFactory;
        _settings = settings;
        _retryPolicyService = retryPolicyService;
        _jobsRegistry = jobsRegistry;
        _serializer = serializer;
        _logger = logger;
        _semaphore = new SemaphoreSlim(settings.MaxDegreeOfParallelism);
    }

    public void StartBackgroundService()
    {
        _running = true;
        if (_settings.UseBatches)
        {
            Task.Run(PollByBatches);
        }
        else
        {
            Task.Run(Poll);
        }
    }

    public void SendStopSignal()
    {
        _running = false;
    }

    private async Task Poll()
    {
        while (_running)
        {
            Job? job = null;
            await _semaphore.WaitAsync();
            try
            {
                job = await _storage.TakeToProcessingAsync();
            }
            catch (Exception ex)
            {
                _semaphore.Release();
                _logger.LogError(ex, "Error receiveng next jobs from queue");
                await Task.Delay(_settings.DbErrorPauseMs);
                continue;
            }

            if (job == null)
            {
                _semaphore.Release();
                if (_running)
                {
                    await Task.Delay(_settings.PollingIntervalMs);
                }
            }
            else
            {
                StartProcessing(job);
            }
        }
    }

    private async Task PollByBatches()
    {
        var jobs = new List<Job>(capacity: _settings.MaxDegreeOfParallelism);
        while (_running)
        {
            await _semaphore.WaitAsync();
            var maxBatchSize = _semaphore.CurrentCount + 1;

            try
            {
                await _storage.TakeBatchToProcessingAsync(maxBatchSize, jobs);
            }
            catch (Exception ex)
            {
                _semaphore.Release();
                _logger.LogError(ex, "Error receiveng next jobs from queue");
                await Task.Delay(_settings.DbErrorPauseMs);
                continue;
            }

            if (jobs.Count == 0)
            {
                _semaphore.Release();
                if (_running)
                {
                    await Task.Delay(_settings.PollingIntervalMs);
                }
            }
            else
            {
                var actualBatchSize = jobs.Count;
                for (int i = 1; i < actualBatchSize; i++)
                {
                    await _semaphore.WaitAsync();
                }

                StartProcessing(jobs);
            }
        }
    }

    private void StartProcessing(Job job)
    {
        Task.Run(() => Process(job));
    }

    private void StartProcessing(IReadOnlyList<Job> jobs)
    {
        for (int i = 0; i < jobs.Count; i++)
        {
            StartProcessing(jobs[i]);
        }
    }

    private async Task Process(Job job)
    {
        try
        {
            if (job.IsRecurrent)
            {
                await ProcessRecurrent(job);
            }
            else
            {
                await ProcessCommand(job);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task ProcessCommand(Job job)
    {
        using var scope = _scopeFactory.CreateJobExecutionScope();
        var retryPolicy = _retryPolicyService.GetRetryPolicy(job);
        var completed = false;
        try
        {
            var execMetadata = _jobsRegistry.GetCommandExecutionMetadata(job.JobName);
            if (execMetadata == null)
            {
                throw new InvalidJobHandlerException($"Job {job.JobName} does not have suitable handler");
            }

            var handlerInstance = scope.GetService(execMetadata.HandlerType);
            if (handlerInstance == null)
            {
                throw new InvalidJobHandlerException($"Could not create instance of handler with type {execMetadata.HandlerType}");
            }

            var command = _serializer.DeserializeJobParam(job.JobParam, execMetadata.CommandType);
            if (command == null)
            {
                throw new InvalidJobHandlerException($"Could not deserialize job parameter with type {execMetadata.CommandType}");
            }

            var ctx = new CommandExecutionContext
            {
                JobName = job.JobName,
                StartedCount = job.StartedCount,
                IsLastAttempt = retryPolicy.IsLastAttempt(job)
            };
            var result = execMetadata.ExecMethod.Invoke(handlerInstance, [command, ctx]);
            if (result is Task)
            {
                await (Task)result;
            }

            completed = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error executing job, jobName = {job.JobName}, id = {job.Id}");

            TimeSpan? retryInterval = retryPolicy.GetIntervalForNextAttempt(job);

            try
            {
                if (retryInterval.HasValue)
                {
                    var sheduledStartTime = DateTime.UtcNow.Add(retryInterval.Value);
                    await _storage.RescheduleAsync(job.Id, sheduledStartTime);
                }
                else
                {
                    await _storage.MarkFailedAsync(job.Id);
                }
            }
            catch (Exception statusEx)
            {
                _logger.LogError(statusEx,
                    "Error while change status of failed job, jobName = {JobName}, id = {JobId}", job.JobName, job.Id);
                // todo: retry status update queue
            }
        }

        if (completed)
        {
            try
            {
                if (_settings.DeleteCompleted)
                {
                    await _storage.DeleteAsync(job.Id);
                }
                else
                {
                    await _storage.MarkCompletedAsync(job.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing executed job, jobName = {JobName}, id = {JobId}", job.JobName, job.Id);
                // todo: retry status update queue
            }
        }

    }

    private async Task ProcessRecurrent(Job job)
    {
        ArgumentNullException.ThrowIfNull(job.Cron, nameof(job.Cron));

        using var scope = _scopeFactory.CreateJobExecutionScope();
        try
        {
            var execMetadata = _jobsRegistry.GetRecurrentJobExecutionMetadata(job.JobName);
            if (execMetadata == null)
            {
                throw new InvalidJobHandlerException($"Job {job.JobName} does not have suitable handler");
            }

            var handlerInstance = scope.GetService(execMetadata.HandlerType);
            if (handlerInstance == null)
            {
                throw new InvalidJobHandlerException($"Could not create instance of handler with type {execMetadata.HandlerType}");
            }

            var ctx = new RecurrentJobExecutionContext
            {
                JobName = job.JobName
            };
            var result = execMetadata.ExecMethod.Invoke(handlerInstance, [ctx]);
            if (result is Task)
            {
                await (Task)result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error executing recurrent job, jobName = {job.JobName}, id = {job.Id}");
        }
        finally
        {
            var nextStartAt = CronHelper.GetNext(job.Cron, DateTime.UtcNow);
            try
            {
                await _storage.RescheduleAsync(job.Id, nextStartAt);
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex,
                    "Error while reschedule next run for recurrent job, jobName = {JobName}, id = {JobId}", job.JobName, job.Id);
                // todo: retry status update queue
            }
        }
    }
}
