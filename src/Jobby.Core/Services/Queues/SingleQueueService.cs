using Jobby.Core.Exceptions;
using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Queues;
using Jobby.Core.Models;

namespace Jobby.Core.Services.Queues;

internal class SingleQueueService<T> : IQueueService<T>
{
    private readonly IQueueItemsReader<T> _queueItemsReader;
    
    private readonly string _serverId;
    private readonly int _maxBatchSize;
    private readonly string _queueName;
    private readonly bool _disableSerializableGroups;
    private readonly GeometryProgression _pollingInterval;

    private bool _isEmpty;
    
    public SingleQueueService(IQueueItemsReader<T> queueItemsReader, QueueServiceConfig config, string serverId)
    {
        if (config.Queues.Count != 1)
            throw new ArgumentException("Queues list must contain single item");
        
        _queueItemsReader = queueItemsReader;
        
        _serverId = serverId;

        var queueConfig = config.Queues[0];
        
        _maxBatchSize = queueConfig.MaxBatchSize;
        _queueName = queueConfig.QueueName;
        _disableSerializableGroups = queueConfig.DisableSerializableGroups;
        
        _pollingInterval = new GeometryProgression(
            start: config.WaitingIntervalStartMs, 
            factor: config.WaitingIntervalFactor, 
            max: config.WaitingIntervalMaxMs
        );
        
        _isEmpty = false;
    }
    
    public int GetWaitingIntervalMs()
    {
        if (_isEmpty)
        {
            var delayMs = _pollingInterval.GetCurrentValueAndSetToNext();
            return delayMs;
        }
        return 0;
    }

    public async Task ReadBatch(int batchSize, List<T> result)
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
        await _queueItemsReader.ReadBatch(request, result);

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