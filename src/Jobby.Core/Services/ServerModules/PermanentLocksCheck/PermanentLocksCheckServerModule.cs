using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Queues;
using Jobby.Core.Interfaces.ServerModules.PermanentLockedGroupsCheck;
using Jobby.Core.Models;
using Microsoft.Extensions.Logging;

namespace Jobby.Core.Services.ServerModules.PermanentLocksCheck;

internal class PermanentLocksCheckServerModule : IPermanentLocksCheckServerModule
{
    private readonly IPermanentLocksStorage _storage;
    private readonly ITimerService _timer;
    private readonly IQueueService<JobWithGroupModel>? _freezingQueue;
    private readonly ILogger<PermanentLocksCheckServerModule> _logger;
    
    private readonly QueueServiceConfig _freezingQueueServiceConfig;
    private readonly JobbyServerSettings _settings;
    
    private CancellationTokenSource _cancellationTokenSource;

    public PermanentLocksCheckServerModule(IPermanentLocksStorage storage,
        IQueueServiceFactory queueServiceFactory,
        ITimerService timer,
        ILogger<PermanentLocksCheckServerModule> logger,
        JobbyServerSettings settings,
        string serverId)
    {
        _storage = storage;
        _timer = timer;
        _logger = logger;
        _settings = settings;
        
        _freezingQueueServiceConfig = new QueueServiceConfig
        {
            WaitingIntervalStartMs = settings.PermanentLockedFreezingIntervalSeconds * 1000,
            WaitingIntervalMaxMs = settings.PermanentLockedFreezingIntervalSeconds * 1000,
            WaitingIntervalFactor = 1,
            Queues = settings.Queues
                .Where(x => !(x.DisableSerializableGroups ?? settings.DisableSerializableGroups ?? false))
                .Select(x => new QueueServiceConfig.QueueConfig
                {
                    QueueName = x.QueueName,
                    DisableSerializableGroups = false,
                    MaxBatchSize = settings.TakeToProcessingBatchSize,
                })
                .ToList()
        };
        if (_freezingQueueServiceConfig.Queues.Count > 0)
        {
            var reader = new FreezePermanentLockedJobsQueueReader(storage);
            _freezingQueue = queueServiceFactory.Create(reader, _freezingQueueServiceConfig, serverId);
        }
        
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public void Start()
    {
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }

        if (_freezingQueueServiceConfig.Queues.Count > 0)
        {
            _ = Task.Run(() => FreezeJobsFromPermanentLockedGroups(_cancellationTokenSource.Token));
            _ = Task.Run(() => HandleUnlockingRequests(_cancellationTokenSource.Token));
        }
        else
        {
            _logger.LogInformation("PermanentLocksCheckServerModule would not be started because serializable groups disabled for all queues");
        }
    }

    public void SendStopSignal()
    {
        _cancellationTokenSource.Cancel();
    }

    private async Task FreezeJobsFromPermanentLockedGroups(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_freezingQueue);
        
        var frozenJobs = new List<JobWithGroupModel>(capacity: _settings.TakeToProcessingBatchSize);
        while (!cancellationToken.IsCancellationRequested)
        {
            var waitingIntervalMs = _freezingQueue.GetWaitingIntervalMs();
            if (waitingIntervalMs > 0)
            {
                await _timer.Delay(waitingIntervalMs, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await _freezingQueue.ReadBatch(_settings.TakeToProcessingBatchSize, frozenJobs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error freezing jobs from permanently locked groups");
                await _timer.Delay(_settings.DbErrorPauseMs, cancellationToken);
                continue;
            }
            
            foreach (var frozenJob in frozenJobs)
            {
                _logger.LogDebug("Job {JobName} with id={JobId} has been frozen because group {GroupId} is permanently locked",
                    frozenJob.JobName, frozenJob.Id, frozenJob.GroupId);
            }
        }
    }

    private async Task HandleUnlockingRequests(CancellationToken cancellationToken)
    {
        var pollingInterval = TimeSpan.FromSeconds(_settings.PermanentLockedHandleUnlockingRequestsIntervalSeconds);
        while (!cancellationToken.IsCancellationRequested)
        {
            GroupUnlockingStatusModel? unlockingStatus = null;
            try
            {
                unlockingStatus = await _storage.UnfreezeBatchAndUnlockIfAllUnfrozen();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling unlocking permanently locked group request");
                await _timer.Delay(_settings.DbErrorPauseMs, cancellationToken);
                continue;
            }

            if (unlockingStatus == null)
            {
                await _timer.Delay(pollingInterval, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                    break;                
            }
            else if (unlockingStatus.IsUnlocked)
            {
                _logger.LogInformation("Serializable group {GroupId} has been unlocked", unlockingStatus.GroupId);
            }
        }
    }
}