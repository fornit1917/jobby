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
    private readonly string _serverId;
    private CancellationTokenSource _cancellationTokenSource;

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
        _serverId = $"{Environment.MachineName}_{Guid.NewGuid()}";
    }

    public void StartBackgroundService()
    {
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }
        _logger.LogInformation("Jobby server is running, serverId = {ServerId}", _serverId);
        Task.Run(Heartbeat);
        Task.Run(Poll);
    }

    public void SendStopSignal()
    {
        _logger.LogInformation("Jobby server received stop signal, serverId = {ServerId}", _serverId);
        _cancellationTokenSource.Cancel();
    }

    private async Task Heartbeat()
    {
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                await _storage.SendHeartbeatAsync(_serverId);
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_settings.HeatbeatIntervalSeconds));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during send hearbeat");
            }
        }
    }

    private async Task Poll()
    {
        var jobs = new List<JobExecutionModel>(capacity: _settings.TakeToProcessingBatchSize);
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
                await _storage.TakeBatchToProcessingAsync(_serverId, maxBatchSize, jobs);
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
    }

    private void Run(JobExecutionModel job)
    {
        Task.Run(() => Execute(job));
    }

    private void Run(IReadOnlyList<JobExecutionModel> jobs)
    {
        for (int i = 0; i < jobs.Count; i++)
        {
            Run(jobs[i]);
        }
    }

    private async Task Execute(JobExecutionModel job)
    {
        try
        {
            await _executionService.ExecuteJob(job, _cancellationTokenSource.Token);
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
