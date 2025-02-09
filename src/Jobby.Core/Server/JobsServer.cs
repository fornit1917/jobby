using Jobby.Abstractions.CommonServices;
using Jobby.Abstractions.Models;
using Jobby.Abstractions.Server;

namespace Jobby.Core.Server;

public class JobsServer : IJobsServer
{
    private readonly IJobsStorage _storage;
    private readonly IJobExecutionScopeFactory _scopeFactory;
    private readonly JobbySettings _settings;

    private readonly SemaphoreSlim _semaphore;

    private bool _running;

    public JobsServer(IJobsStorage storage, IJobExecutionScopeFactory scopeFactory, JobbySettings settings)
    {
        _storage = storage;
        _scopeFactory = scopeFactory;
        _settings = settings;
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
            try
            {
                var executor = scope.GetJobExecutor(job.JobName);
                await executor.ExecuteAsync(job);
            }
            catch (Exception ex)
            {
                // todo: do not use hardcoded retry policy
                if (job.StartedCount >= 10)
                {
                    await _storage.MarkFailedAsync(job.Id);
                }
                else
                {
                    var sheduledStartTime = DateTime.UtcNow.AddMinutes(10);
                    await _storage.RescheduleAsync(job.Id, sheduledStartTime);
                }
                return;
            }

            //todo: log if error
            await _storage.MarkCompletedAsync(job.Id);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
