namespace Jobby.Core.Models;

public readonly record struct RecurrentJobOpts
{
    public DateTime? StartTime { get; init; }
    public string? QueueName { get; init; }
    public string? SerializableGroupId { get; init; }
    public bool? CanBeRestartedIfServerGoesDown { get; init; }
}