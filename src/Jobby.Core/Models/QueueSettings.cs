namespace Jobby.Core.Models;

public class QueueSettings
{
    public const string DefaultQueueName = "default";
    
    public required string QueueName { get; init; }
    public int MaxDegreeOfParallelism { get; init; }
}