using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;

namespace Jobby.Dashboard.UI.Shared;

// The backlog of a queue is its unfinished jobs: everything except Completed and Failed.
public static class QueueBacklog
{
    public static IReadOnlyList<JobStatus> Statuses { get; } =
        [JobStatus.Scheduled, JobStatus.Processing, JobStatus.WaitingPrev, JobStatus.Frozen];

    public static long Count(IReadOnlyList<DashboardStatusCount> statusCounts) =>
        statusCounts.Where(c => Statuses.Contains(c.Status)).Sum(c => c.Count);
}