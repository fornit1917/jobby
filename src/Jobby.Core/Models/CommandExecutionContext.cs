namespace Jobby.Core.Models;

public class CommandExecutionContext
{
    public required string JobName { get; init; }
    public required int StartedCount { get; init; }
    public required bool IsLastAttempt { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}
