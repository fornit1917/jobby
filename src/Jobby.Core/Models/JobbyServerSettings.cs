namespace Jobby.Core.Models;

public class JobbyServerSettings
{
    private int _pollingIntervalMs = 1000;
    public int PollingIntervalMs 
    { 
        get => _pollingIntervalMs;
        init
        {
            ThrowIfValueIsNotPositive(value, nameof(PollingIntervalMs));
            _pollingIntervalMs = value;
        }
    }

    private int _dbErrorPauseMs = 5000;
    public int DbErrorPauseMs 
    {
        get => _dbErrorPauseMs; 
        init
        {
            ThrowIfValueIsNotPositive(value, nameof(DbErrorPauseMs));
            _dbErrorPauseMs = value;
        }
    }
    
    private int _maxDegreeOfParallelism = Environment.ProcessorCount + 1;
    public int MaxDegreeOfParallelism 
    { 
        get => _maxDegreeOfParallelism;
        init
        {
            ThrowIfValueIsNotPositive(value, nameof(MaxDegreeOfParallelism));
            _maxDegreeOfParallelism = value;
        }
    }
    
    private int _takeToProcessingBatchSize = Environment.ProcessorCount <= 9 ? Environment.ProcessorCount + 1 : 10;
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

    private int _heartbeatIntervalSeconds = 10;
    public int HeartbeatIntervalSeconds
    {
        get => _heartbeatIntervalSeconds;
        init
        {
            ThrowIfValueIsNotPositive(value, nameof(HeartbeatIntervalSeconds));
            _heartbeatIntervalSeconds = value;
        }
    }

    private int _maxNoHeartbeatIntervalSeconds = 300;
    public int MaxNoHeartbeatIntervalSeconds
    {
        get => _maxNoHeartbeatIntervalSeconds;
        init
        {
            ThrowIfValueIsNotPositive(value, nameof(MaxNoHeartbeatIntervalSeconds));
            _maxNoHeartbeatIntervalSeconds = value;
        }
    }

    private static void ThrowIfValueIsNotPositive(int value, string fieldName)
    {
        if (value <= 0)
        {
            throw new ArgumentException($"The {fieldName} property value should be positive. Given: {value}.");
        }
    }
}
