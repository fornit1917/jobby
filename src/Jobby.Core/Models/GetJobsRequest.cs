namespace Jobby.Core.Models;

public readonly record struct GetJobsRequest
{
    public required string QueueName { get; init; }
    public required int BatchSize { get; init; }
    public required string ServerId { get; init; }
    public bool DisableSerializableGroups { get; init; }
}