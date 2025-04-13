namespace Jobby.Core.Models;

public class RecurrentJobExecutionContext
{
    public required string JobName { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}
