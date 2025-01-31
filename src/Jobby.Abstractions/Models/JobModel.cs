namespace Jobby.Abstractions.Models;

public class JobModel
{
    public long Id { get; set; }

    public string JobName { get; set; } = string.Empty;

    public string? JobParam { get; set; }

    public JobStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ScheduledStartAt { get; set; }
    public DateTime? LastStartedAt { get; set; }
    public DateTime? LastFinishedAt { get; set; }

    public int StartedCount { get; set; }

    public string? RecurrentJobKey { get; set; }
    public string? Cron { get; set; }
}
