﻿using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Core.Services;

public class JobsServer : IJobsServer
{
    private readonly IJobsStorage _storage;
    private readonly IJobExecutionScopeFactory _scopeFactory;
    private readonly IRetryPolicyService _retryPolicyService;
    private readonly IJobsRegistry _jobsRegistry;
    private readonly IJobParamSerializer _serializer;

    private readonly JobbySettings _settings;

    private readonly SemaphoreSlim _semaphore;

    private bool _running;

    public JobsServer(IJobsStorage storage,
        IJobExecutionScopeFactory scopeFactory,
        IRetryPolicyService retryPolicyService,
        IJobsRegistry jobsRegistry,
        IJobParamSerializer serializer,
        JobbySettings settings)
    {
        _storage = storage;
        _scopeFactory = scopeFactory;
        _settings = settings;
        _retryPolicyService = retryPolicyService;
        _jobsRegistry = jobsRegistry;
        _serializer = serializer;
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
            JobModel? job = null;
            await _semaphore.WaitAsync();
            try
            {
                job = await _storage.TakeToProcessingAsync();
            }
            catch
            {
                _semaphore.Release();
                // todo: log error
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
        var jobs = new List<JobModel>(capacity: _settings.MaxDegreeOfParallelism);
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
                // todo: log error
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

    private void StartProcessing(JobModel job)
    {
        Task.Run(() => Process(job));
    }

    private void StartProcessing(IReadOnlyList<JobModel> jobs)
    {
        for (int i = 0; i < jobs.Count; i++)
        {
            StartProcessing(jobs[i]);
        }
    }

    private async Task Process(JobModel job)
    {
        try
        {
            using var scope = _scopeFactory.CreateJobExecutionScope();
            var retryPolicy = _retryPolicyService.GetRetryPolicy(job);
            var completed = false;
            try
            {
                var execMetadata = _jobsRegistry.GetJobExecutionMetadata(job.JobName);
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

                var ctx = new JobExecutionContext
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
                TimeSpan? retryInterval = retryPolicy.GetIntervalForNextAttempt(job);

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

            if (completed)
            {
                await _storage.MarkCompletedAsync(job.Id);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
