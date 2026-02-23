using Jobby.Core.Exceptions;
using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Queues;
using Jobby.Core.Models;

namespace Jobby.Core.Services.Queues;

internal class MultiQueueService<T> : IQueueService<T>
{
    private readonly IQueueItemsReader<T> _queueItemsReader;
    private readonly ITimerService _timer;
    private readonly string _serverId;

    private record QueueInfo(string QueueName, int MaxBatchSize, bool DisableSerializableGroups)
    {
        public long LastRequestTs { get; set; }
    };
    
    private readonly Queue<QueueInfo> _hotWaitingList;
    private readonly Queue<QueueInfo> _coldWaitingList;
    private QueueInfo? _current;
    private int _totalReceivedFromCurrent;

    private readonly GeometryProgression _pollingInterval;

    public MultiQueueService(IQueueItemsReader<T> queueItemsReader,
        ITimerService timer,
        QueueServiceConfig config,
        string serverId)
    {
        if (config.Queues.Count == 0)
            throw new ArgumentException("Queues cannot be empty");

        _queueItemsReader = queueItemsReader;
        _timer = timer;
        _serverId = serverId;
        
        _hotWaitingList = new Queue<QueueInfo>(capacity: config.Queues.Count);
        _coldWaitingList = new Queue<QueueInfo>(capacity: config.Queues.Count);
        foreach (var q in config.Queues)
        {
            var queueInfo = new QueueInfo(q.QueueName, q.MaxBatchSize, q.DisableSerializableGroups);
            _hotWaitingList.Enqueue(queueInfo);
        }
        
        SetCurrent(_hotWaitingList.Dequeue());
        
        _pollingInterval = new GeometryProgression(
            start: config.WaitingIntervalStartMs, 
            factor: config.WaitingIntervalFactor, 
            max: config.WaitingIntervalMaxMs
        );
    }

    public int GetWaitingIntervalMs()
    {
        if (_current != null || _hotWaitingList.Count > 0 || _coldWaitingList.Count == 0)
        {
            return 0;
        }

        var coldHead = _coldWaitingList.Peek();
        if (coldHead.LastRequestTs == 0)
        {
            return 0;
        }
        
        var passedFromLastRequestMs = _timer.GetElapsedTime(coldHead.LastRequestTs).TotalMilliseconds;
        if (passedFromLastRequestMs >= _pollingInterval.CurrentValue)
        {
            return 0;
        }

        var delayMs = (int)(_pollingInterval.GetCurrentValueAndSetToNext() - passedFromLastRequestMs);
        if (delayMs > 0)
        {
            return delayMs;
        }
        return 0;
    }

    public async Task ReadBatch(int batchSize, List<T> result)
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
        await _queueItemsReader.ReadBatch(request, result);
        
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