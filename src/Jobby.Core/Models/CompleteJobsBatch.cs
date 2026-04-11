namespace Jobby.Core.Models;

public readonly record struct CompleteJobsBatch
{
    public string ServerId { get; init; }
    public IReadOnlyList<Guid> JobIds { get; init; }
    public IReadOnlyList<Guid> NextJobIds { get; init; }
}