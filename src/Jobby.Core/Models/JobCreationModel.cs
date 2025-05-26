namespace Jobby.Core.Models;

public class JobCreationModel
{
    public Guid Id { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string? JobParam { get; set; }
    public string? Cron { get; set; }
    public JobStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ScheduledStartAt { get; set; }
    public Guid? NextJobId { get; set; }
    public bool CanBeRestarted { get; set; }
}
