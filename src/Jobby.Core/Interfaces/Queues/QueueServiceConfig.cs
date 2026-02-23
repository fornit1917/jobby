using Jobby.Core.Models;

namespace Jobby.Core.Interfaces.Queues;

internal class QueueServiceConfig
{
    public IReadOnlyList<QueueConfig> Queues { get; init; } = Array.Empty<QueueConfig>();
    
    public int WaitingIntervalMaxMs { get; init; }
    public int WaitingIntervalStartMs { get; init; }
    public int WaitingIntervalFactor { get; init; }

    public class QueueConfig
    {
        public string QueueName { get; init; } = QueueSettings.DefaultQueueName;
        public int MaxBatchSize { get; init; }
        public bool DisableSerializableGroups { get; init; }
    }
}