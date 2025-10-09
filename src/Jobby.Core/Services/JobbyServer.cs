using Jobby.Core.Helpers;
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
    public string ServerId { get; private init; }
    private CancellationTokenSource _cancellationTokenSource;

    public JobbyServer(IJobbyStorage storage,
        IJobExecutionService executionService,
        IJobPostProcessingService postProcessingService,
        ILogger<JobbyServer> logger,
        JobbyServerSettings settings,
        string serverId)
    {
        _storage = storage;
        _executionService = executionService;
        _postProcessingService = postProcessingService;
        _settings = settings;
        _logger = logger;

        _semaphore = new SemaphoreSlim(settings.MaxDegreeOfParallelism);
        _cancellationTokenSource = new CancellationTokenSource();
        ServerId = serverId;
    }

    public void StartBackgroundService()
    {
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }
        _logger.LogInformation("Jobby server is running, serverId = {ServerId}", ServerId);
        Task.Run(SendHeartbeatAndProcessLostServers);
        Task.Run(Poll);
    }

    public void SendStopSignal()
    {
        _logger.LogInformation("Jobby server received stop signal, serverId = {ServerId}", ServerId);
        _cancellationTokenSource.Cancel();
    }

    private async Task SendHeartbeatAndProcessLostServers()
    {
        List<string> deletedServerIds = new List<string>();
        List<StuckJobModel> stuckJobs = new List<StuckJobModel>();

        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            // send heartbeat
            try
            {
                await _storage.SendHeartbeatAsync(ServerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during send heartbeat");
            }

            // detect lost servers and restart their jobs
            try
            {
                var minLastHearbeat = DateTime.UtcNow.AddSeconds(-1 * _settings.MaxNoHeartbeatIntervalSeconds);
                await _storage.DeleteLostServersAndRestartTheirJobsAsync(minLastHearbeat, deletedServerIds, stuckJobs);
                foreach (var serverId in deletedServerIds)
                {
                    _logger.LogInformation("Lost server was found and deleted, serverId = {ServerId}", serverId);
                }
                foreach (var job in stuckJobs)
                {
                    if (job.CanBeRestarted)
                    {
                        _logger.LogInformation(
                            "Job was restarted because server did not send hearbeat, jobName = {JobName}, id = {JobId}",
                            job.JobName, job.Id);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Probably job got stuck and can not be restarted automatically, its server did not send heartbeat, jobName = {JobName}, id = {JobId}",
                            job.JobName, job.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during detect lost servers and restart their jobs");
            }

            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(_settings.HeartbeatIntervalSeconds));
            }
        }
    }

    private async Task Poll()
    {
        var jobs = new List<JobExecutionModel>(capacity: _settings.TakeToProcessingBatchSize);
        var pollingInterval = new GeometryProgression(
            start: _settings.PollingIntervalStartMs, 
            factor: _settings.PollingIntervalFactor, 
            max: _settings.PollingIntervalMs
        );

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
                await _storage.TakeBatchToProcessingAsync(ServerId, maxBatchSize, jobs);
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
                    await Task.Delay(pollingInterval.GetNextValue());
                }
            }
            else
            {
                pollingInterval.Reset();
                
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
