using Jobby.Core.Models;

namespace Jobby.Dashboard.Client.Shared;

public static class QueueBacklog
{
    public static IReadOnlyList<JobStatus> Statuses { get; } =
        [JobStatus.Scheduled, JobStatus.Processing, JobStatus.WaitingPrev, JobStatus.Frozen];
}