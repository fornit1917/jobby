namespace Jobby.Core.Models;

public class JobbyServerSettings
{
    private readonly int _pollingIntervalMs = 1000;
    public int PollingIntervalMs 
    { 
        get => _pollingIntervalMs;
        init
        {
            ThrowIfValueIsNotPositive(value, nameof(PollingIntervalMs));
            _pollingIntervalMs = value;
        }
    }

    private readonly int _pollingIntervalStartMs = 0;
    public int PollingIntervalStartMs
    {
        get => _pollingIntervalStartMs > 0 ? _pollingIntervalStartMs : _pollingIntervalMs;
        init
        {
            ThrowIfValueIsNotPositive(value, nameof(PollingIntervalStartMs));
            _pollingIntervalStartMs = value;
        }
    }

    private readonly int _pollingIntervalFactor = 2;
    public int PollingIntervalFactor
    {
        get => _pollingIntervalFactor;
        init 
        {
            if (value < 1)
                throw new ArgumentException("The PollingIntervalFactor property value should be not less than 1");
            _pollingIntervalFactor = value;
        }
    }

    private readonly int _dbErrorPauseMs = 5000;
    public int DbErrorPauseMs 
    {
        get => _dbErrorPauseMs; 
        init
        {
            ThrowIfValueIsNotPositive(value, nameof(DbErrorPauseMs));
            _dbErrorPauseMs = value;
        }
    }
    
    private readonly int _maxDegreeOfParallelism = Environment.ProcessorCount + 1;
    public int MaxDegreeOfParallelism 
    { 
        get => _maxDegreeOfParallelism;
        init
        {
            ThrowIfValueIsNotPositive(value, nameof(MaxDegreeOfParallelism));
            _maxDegreeOfParallelism = value;
        }
    }
    
    private readonly int _takeToProcessingBatchSize = Environment.ProcessorCount + 1;
    public int TakeToProcessingBatchSize 
    {
        get => _takeToProcessingBatchSize;
        init
        {
            ThrowIfValueIsNotPositive(value, nameof(TakeToProcessingBatchSize));
            _takeToProcessingBatchSize = value;
        }
    }
    
    public bool DeleteCompleted { get; init; } = true;
    
    public bool CompleteWithBatching { get; init; } = false;

    private readonly int _heartbeatIntervalSeconds = 10;
    public int HeartbeatIntervalSeconds
    {
        get => _heartbeatIntervalSeconds;
        init
        {
            ThrowIfValueIsNotPositive(value, nameof(HeartbeatIntervalSeconds));
            _heartbeatIntervalSeconds = value;
        }
    }

    private readonly int _maxNoHeartbeatIntervalSeconds = 300;
    public int MaxNoHeartbeatIntervalSeconds
    {
        get => _maxNoHeartbeatIntervalSeconds;
        init
        {
            ThrowIfValueIsNotPositive(value, nameof(MaxNoHeartbeatIntervalSeconds));
            _maxNoHeartbeatIntervalSeconds = value;
        }
    }

    public IReadOnlyList<QueueSettings> Queues { get; init; } =
    [
        new() { QueueName = QueueSettings.DefaultQueueName }
    ];

    private static void ThrowIfValueIsNotPositive(int value, string fieldName)
    {
        if (value <= 0)
        {
            throw new ArgumentException($"The {fieldName} property value should be positive. Given: {value}.");
        }
    }
}
