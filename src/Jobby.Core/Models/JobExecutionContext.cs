namespace Jobby.Core.Models;

public class JobExecutionContext
{
    public required string JobName { get; init; }
    public required int StartedCount { get; init; }
    public required bool IsLastAttempt { get; init; }
}
