using Jobby.Core.Services.Schedulers;

namespace Jobby.Core.Models;

public class JobCreationModel
{
    public Guid Id { get; internal set; }
    public string JobName { get; internal set; } = string.Empty;
    public string? JobParam { get; internal set; }
    public string? Schedule { get; internal set; }
    public string? SchedulerType { get; internal set; }
    public bool IsExclusive { get; internal set; }
    public JobStatus Status { get; internal set; }
    public DateTime CreatedAt { get; internal set; }
    public DateTime ScheduledStartAt { get; internal set; }
    public Guid? NextJobId { get; internal set; }
    public bool CanBeRestarted { get; internal set; } = true;
    public string QueueName { get; internal set; } = QueueSettings.DefaultQueueName;
    public string? SerializableGroupId  { get; internal set; }
    public bool LockGroupIfFailed { get; internal set; }

    internal JobCreationModel()
    {
    }
}
