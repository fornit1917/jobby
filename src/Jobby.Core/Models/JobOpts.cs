namespace Jobby.Core.Models;

public readonly record struct JobOpts
{
    public DateTime? StartTime { get; init; }
    public string? QueueName { get; init; }
}