using Jobby.Core.Exceptions;
using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Queues;
using Jobby.Core.Models;

namespace Jobby.Core.Services.Queues;

internal class SingleQueueService : IQueueService
{
    private readonly IJobbyStorage _storage;
    private readonly ITimerService _timer;
    
    private readonly string _serverId;
    private readonly int _maxBatchSize;
    private readonly string _queueName;
    private readonly bool _disableSerializableGroups;
    private readonly JobbyServerSettings _settings;
    private readonly GeometryProgression _pollingInterval;

    private bool _isEmpty;
    
    public SingleQueueService(IJobbyStorage storage, ITimerService timer, JobbyServerSettings settings, string serverId)
    {
        if (settings.Queues.Count != 1)
            throw new InvalidBuilderConfigException("Queues list must contain single item");
        
        _serverId = serverId;
        _storage = storage;
        _timer = timer;
        _settings = settings;

        var queueSettings = settings.Queues.First();
        
        _maxBatchSize = queueSettings.MaxDegreeOfParallelism > 0 && queueSettings.MaxDegreeOfParallelism <= settings.MaxDegreeOfParallelism
            ? queueSettings.MaxDegreeOfParallelism
            : settings.MaxDegreeOfParallelism;
        
        _queueName = queueSettings.QueueName;

        _disableSerializableGroups =
            queueSettings.DisableSerializableGroups ?? settings.DisableSerializableGroups ?? false;
        
        _pollingInterval = new GeometryProgression(
            start: settings.PollingIntervalStartMs, 
            factor: settings.PollingIntervalFactor, 
            max: settings.PollingIntervalMs
        );
        
        _isEmpty = false;
    }
    
    public Task WaitIfEmpty()
    {
        if (_isEmpty)
        {
            var delayMs = _pollingInterval.GetCurrentValueAndSetToNext();
            return _timer.Delay(delayMs);
        }
        return Task.CompletedTask;
    }

    public async Task TakeBatchToProcessing(int batchSize, List<JobExecutionModel> result)
    {
        if (batchSize > _maxBatchSize)
        {
            batchSize = _maxBatchSize;
        }
        
        result.Clear();
        var request = new GetJobsRequest
        {
            QueueName = _queueName,
            BatchSize = batchSize,
            ServerId = _serverId,
            DisableSerializableGroups = _disableSerializableGroups,
        };
        await _storage.TakeBatchToProcessingAsync(request, result);

        if (result.Count == 0)
        {
            _isEmpty = true;
        }
        else
        {
            _isEmpty = false;
            _pollingInterval.Reset();
        }
    }
}