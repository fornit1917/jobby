namespace Jobby.Core.Models;

public readonly record struct JobExecutionContext
{
    public required string JobName { get; init; }
    public required int StartedCount { get; init; }
    public required bool IsLastAttempt { get; init; }
    public required bool IsRecurrent { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}
