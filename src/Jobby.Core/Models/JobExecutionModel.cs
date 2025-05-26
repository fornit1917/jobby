namespace Jobby.Core.Models;

public class JobExecutionModel
{
    public Guid Id { get; init; }
    public string JobName { get; init; } = string.Empty;
    public string? Cron { get; init; }
    public string? JobParam { get; init; }
    public int StartedCount { get; init; }
    public Guid? NextJobId { get; init; }

    public bool IsRecurrent => Cron != null;
}
