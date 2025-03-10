namespace Jobby.Core.Models;

public class Job
{
    public Guid Id { get; set; }

    public string JobName { get; set; } = string.Empty;
    public string? Cron { get; set; }

    public string? JobParam { get; set; }

    public JobStatus Status { get; set; }
    public int StartedCount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ScheduledStartAt { get; set; }
    public DateTime? LastStartedAt { get; set; }
    public DateTime? LastFinishedAt { get; set; }

    public Guid? NextJobId { get; set; }

    public bool IsRecurrent => Cron != null;
}
