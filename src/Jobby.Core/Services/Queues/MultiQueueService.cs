using Jobby.Core.Exceptions;
using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Queues;
using Jobby.Core.Models;

namespace Jobby.Core.Services.Queues;

internal class MultiQueueService : IQueueService
{
    private readonly IJobbyStorage _storage;
    private readonly ITimerService _timer;
    private readonly string _serverId;
    private readonly JobbyServerSettings _settings;

    private record QueueInfo(string QueueName, int MaxBatchSize, bool DisableSerializableGroups)
    {
        public long LastRequestTs { get; set; }
    };
    
    private readonly Queue<QueueInfo> _hotWaitingList;
    private readonly Queue<QueueInfo> _coldWaitingList;
    private QueueInfo? _current;
    private int _totalReceivedFromCurrent;

    private readonly GeometryProgression _pollingInterval;

    public MultiQueueService(IJobbyStorage storage,
        ITimerService timer,
        JobbyServerSettings settings,
        string serverId)
    {
        if (settings.Queues.Count == 0)
            throw new InvalidBuilderConfigException("Queues cannot be empty");
        
        _storage = storage;
        _timer = timer;
        _serverId = serverId;
        _settings = settings;
        
        _hotWaitingList = new Queue<QueueInfo>(capacity: settings.Queues.Count);
        _coldWaitingList = new Queue<QueueInfo>(capacity: settings.Queues.Count);
        foreach (var queueSettings in settings.Queues)
        {
            var maxBatchSize = queueSettings.MaxDegreeOfParallelism > 0 && queueSettings.MaxDegreeOfParallelism <= settings.MaxDegreeOfParallelism
                ? queueSettings.MaxDegreeOfParallelism
                : settings.MaxDegreeOfParallelism;
            var disableSerializableGroups = queueSettings.DisableSerializableGroups 
                                            ?? _settings.DisableSerializableGroups 
                                            ?? false;
            var queueInfo = new QueueInfo(queueSettings.QueueName, maxBatchSize, disableSerializableGroups);
            _hotWaitingList.Enqueue(queueInfo);
        }
        
        SetCurrent(_hotWaitingList.Dequeue());
        
        _pollingInterval = new GeometryProgression(
            start: settings.PollingIntervalStartMs, 
            factor: settings.PollingIntervalFactor, 
            max: settings.PollingIntervalMs
        );
    }

    public Task WaitIfEmpty()
    {
        if (_current != null || _hotWaitingList.Count > 0 || _coldWaitingList.Count == 0)
        {
            return Task.CompletedTask;
        }

        var coldHead = _coldWaitingList.Peek();
        if (coldHead.LastRequestTs == 0)
        {
            return Task.CompletedTask;
        }
        
        var passedFromLastRequestMs = _timer.GetElapsedTime(coldHead.LastRequestTs).TotalMilliseconds;
        if (passedFromLastRequestMs >= _pollingInterval.CurrentValue)
        {
            return Task.CompletedTask;
        }

        var delayMs = (int)(_pollingInterval.GetCurrentValueAndSetToNext() - passedFromLastRequestMs);
        if (delayMs > 0)
        {
            return _timer.Delay(delayMs);
        }
        return Task.CompletedTask;
    }

    public async Task TakeBatchToProcessing(int batchSize, List<JobExecutionModel> result)
    {
        result.Clear();
        
        if (_current == null)
        {
            ChooseCurrentQueueFromWaitingList();
        }

        if (batchSize > _current!.MaxBatchSize)
        {
            batchSize = _current.MaxBatchSize;
        }

        var request = new GetJobsRequest
        {
            QueueName = _current.QueueName,
            BatchSize = batchSize,
            ServerId = _serverId,
            DisableSerializableGroups = _current.DisableSerializableGroups
        };
        await _storage.TakeBatchToProcessingAsync(request, result);
        _current.LastRequestTs = _timer.GetCurrentTicks();
        
        if (result.Count == 0)
        {
            _coldWaitingList.Enqueue(_current);
            SetCurrent(null);
        }
        else
        {
            _pollingInterval.Reset();
            _totalReceivedFromCurrent += result.Count;
            if (result.Count < batchSize || _totalReceivedFromCurrent >= _current.MaxBatchSize)
            {
                _hotWaitingList.Enqueue(_current);
                SetCurrent(null);
            }
        }
    }

    private void SetCurrent(QueueInfo? queueInfo)
    {
        _current = queueInfo;
        _totalReceivedFromCurrent = 0;
    }

    private void ChooseCurrentQueueFromWaitingList()
    {
        if (_hotWaitingList.Count > 0 && _coldWaitingList.Count == 0)
        {
            SetCurrent(_hotWaitingList.Dequeue());
        }
        else if (_hotWaitingList.Count > 0 && _coldWaitingList.Count > 0)
        {
            var coldHead = _coldWaitingList.Peek();
            var hotHead = _hotWaitingList.Peek();
            if (coldHead.LastRequestTs < hotHead.LastRequestTs
                && _timer.GetElapsedTime(coldHead.LastRequestTs).TotalMilliseconds >= _pollingInterval.CurrentValue)
            {
                SetCurrent(_coldWaitingList.Dequeue());
            }
            else
            {
                SetCurrent(_hotWaitingList.Dequeue());
            }
        }
        else
        {
            SetCurrent(_coldWaitingList.Dequeue());
        }        
    }
}