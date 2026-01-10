namespace Jobby.Core.Models;

public readonly record struct JobCreationOptions
{
    public DateTime? StartTime { get; init; }
    public string? SequenceId { get; init; }
}
