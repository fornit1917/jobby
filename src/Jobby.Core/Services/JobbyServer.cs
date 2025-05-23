using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Microsoft.Extensions.Logging;

namespace Jobby.Core.Services;

internal class JobbyServer : IJobbyServer, IDisposable
{
    private readonly IJobbyStorage _storage;
    private readonly IJobExecutionService _executionService;
    private readonly IJobPostProcessingService _postProcessingService;
    private readonly ILogger<JobbyServer> _logger;
    private readonly JobbyServerSettings _settings;

    private readonly SemaphoreSlim _semaphore;
    private CancellationTokenSource _cancellationTokenSource;
    private bool _polling = false;

    public JobbyServer(IJobbyStorage storage,
        IJobExecutionService executionService,
        IJobPostProcessingService postProcessingService,
        ILogger<JobbyServer> logger,
        JobbyServerSettings settings
        )
    {
        _storage = storage;
        _executionService = executionService;
        _postProcessingService = postProcessingService;
        _settings = settings;
        _logger = logger;

        _semaphore = new SemaphoreSlim(settings.MaxDegreeOfParallelism);
        _cancellationTokenSource = new CancellationTokenSource();
        _executionService = executionService;
    }

    public void StartBackgroundService()
    {
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }
        Task.Run(Poll);
    }

    public void SendStopSignal()
    {
        _cancellationTokenSource.Cancel();
    }

    private async Task Poll()
    {
        _polling = true;
        var jobs = new List<Job>(capacity: _settings.TakeToProcessingBatchSize);
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            await _semaphore.WaitAsync();

            try
            {
                if (!_postProcessingService.IsRetryQueueEmpty)
                {
                    await Task.Delay(_settings.DbErrorPauseMs);
                    await _postProcessingService.DoRetriesFromQueue();
                }
            }
            catch (Exception ex)
            {
                _semaphore.Release();
                _logger.LogError(ex, "Error while retry post-processing for jobs");
                continue;
            }

            var maxBatchSize = _semaphore.CurrentCount + 1;
            if (maxBatchSize > _settings.TakeToProcessingBatchSize)
            {
                maxBatchSize = _settings.TakeToProcessingBatchSize;
            }

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
                if (!_cancellationTokenSource.IsCancellationRequested)
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

                Run(jobs);
            }
        }
        _polling = false;
    }

    private void Run(Job job)
    {
        Task.Run(() => Execute(job));
    }

    private void Run(IReadOnlyList<Job> jobs)
    {
        for (int i = 0; i < jobs.Count; i++)
        {
            Run(jobs[i]);
        }
    }

    private async Task Execute(Job job)
    {
        try
        {
            if (job.IsRecurrent)
            {
                await _executionService.ExecuteRecurrent(job, _cancellationTokenSource.Token);
            }
            else
            {
                await _executionService.ExecuteCommand(job, _cancellationTokenSource.Token);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        _executionService.Dispose();

        if (!_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }
    }

    public bool HasInProgressJobs()
    {
        return _semaphore.CurrentCount < _settings.MaxDegreeOfParallelism;
    }
}
