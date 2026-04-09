using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Queues;
using Jobby.Core.Interfaces.ServerModules.JobsExecution;
using Jobby.Core.Models;
using Microsoft.Extensions.Logging;

namespace Jobby.Core.Services.ServerModules.JobsExecution;

internal class JobsExecutionServerModule : IJobsExecutionServerModule
{
    private readonly IQueueService<JobExecutionModel> _queueService;
    private readonly IJobExecutionService _executionService;
    private readonly IJobPostProcessingService _postProcessingService;
    private readonly ITimerService _timer;
    private readonly ILogger<JobsExecutionServerModule> _logger;
    private readonly JobbyServerSettings _settings;
    
    private readonly SemaphoreSlim _semaphore;
    private CancellationTokenSource _cancellationTokenSource;

    public JobsExecutionServerModule(IJobbyStorage storage,
        IQueueServiceFactory queueServiceFactory, 
        IJobExecutionService executionService,
        IJobPostProcessingService postProcessingService,
        ITimerService timer,
        ILogger<JobsExecutionServerModule> logger,
        JobbyServerSettings settings,
        string serverId)
    {
        var queueServiceConfig = new QueueServiceConfig
        {
            WaitingIntervalStartMs = settings.PollingIntervalStartMs,
            WaitingIntervalFactor = settings.PollingIntervalFactor,
            WaitingIntervalMaxMs = settings.PollingIntervalMs,
            Queues = settings.Queues.Select(q => new QueueServiceConfig.QueueConfig
            {
                QueueName = q.QueueName,
                MaxBatchSize = q.MaxDegreeOfParallelism > 0 &&
                               q.MaxDegreeOfParallelism < settings.MaxDegreeOfParallelism
                    ? q.MaxDegreeOfParallelism
                    : settings.MaxDegreeOfParallelism,
                DisableSerializableGroups = q.DisableSerializableGroups ?? settings.DisableSerializableGroups ?? false
            }).ToList()
        };
        var queueReader = new TakeToProcessingJobsQueueReader(storage);
        _queueService = queueServiceFactory.Create<JobExecutionModel>(queueReader, queueServiceConfig, serverId);
        
        _executionService = executionService;
        _postProcessingService = postProcessingService;
        _timer = timer;
        _logger = logger;
        _settings = settings;
        
        _semaphore = new SemaphoreSlim(settings.MaxDegreeOfParallelism);
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public void Start()
    {
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }
        
        _ = Task.Run(() => Poll(_cancellationTokenSource.Token));
    }

    public void SendStopSignal()
    {
        _cancellationTokenSource.Cancel();
    }
    
    public bool HasInProgressJobs()
    {
        return _semaphore.CurrentCount < _settings.MaxDegreeOfParallelism;
    }
    
    private async Task Poll(CancellationToken cancellationToken)
    {
        var jobs = new List<JobExecutionModel>(capacity: _settings.TakeToProcessingBatchSize);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _semaphore.WaitAsync(cancellationToken);
            }
            catch (TaskCanceledException)
            {
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _semaphore.Release();
                break;
            }
            
            try
            {
                if (!_postProcessingService.IsRetryQueueEmpty)
                {
                    await _timer.Delay(_settings.DbErrorPauseMs, cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _semaphore.Release();
                        break;
                    }
                    
                    await _postProcessingService.DoRetriesFromQueue(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while retry post-processing for jobs");
                continue;
            }

            var waitingIntervalMs = _queueService.GetWaitingIntervalMs();
            if (waitingIntervalMs > 0)
            {
                await _timer.Delay(waitingIntervalMs, cancellationToken);
            }
            if (cancellationToken.IsCancellationRequested)
            {
                _semaphore.Release();
                break;
            }

            var batchSize = _semaphore.CurrentCount + 1;
            if (batchSize > _settings.TakeToProcessingBatchSize)
            {
                batchSize = _settings.TakeToProcessingBatchSize;
            }

            try
            {
                await _queueService.ReadBatch(batchSize, jobs);
            }
            catch (Exception ex)
            {
                _semaphore.Release();
                _logger.LogError(ex, "Error receiving next jobs from queue");
                await Task.Delay(_settings.DbErrorPauseMs, cancellationToken);
                continue;
            }

            if (jobs.Count == 0)
            {
                _semaphore.Release();
            }
            else
            {
                var actualBatchSize = jobs.Count;
                for (int i = 1; i < actualBatchSize; i++)
                {
                    await _semaphore.WaitAsync(cancellationToken);
                }
                Run(jobs, cancellationToken);
            }
        }
    }

    private void Run(IReadOnlyList<JobExecutionModel> jobs, CancellationToken cancellationToken)
    {
        foreach (var job in jobs)
        {
            Run(job, cancellationToken);
        }
    }
    
    private void Run(JobExecutionModel job, CancellationToken cancellationToken)
    {
        _ = Task.Run(() => Execute(job, cancellationToken), CancellationToken.None);
    }

    private async Task Execute(JobExecutionModel job, CancellationToken cancellationToken)
    {
        try
        {
            await _executionService.ExecuteJob(job, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}