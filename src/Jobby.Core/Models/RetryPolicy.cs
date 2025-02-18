namespace Jobby.Core.Models;

public class RetryPolicy
{
    public int MaxCount { get; init; }
    public IReadOnlyList<int> IntervalsSeconds { get; init; } = Array.Empty<int>();
}
