using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;

namespace Jobby.Postgres.Dashboard;

// Number of non-recurrent jobs per queue and status, read by one aggregate query.
// Statistics, queue cards and totals of job lists filtered only by queue and status are derived from it.
internal sealed class QueueStatusCounts
{
    public QueueStatusCounts(IReadOnlyList<DashboardQueueDto> queues)
    {
        Queues = queues;
        Stats = new DashboardStatsDto(queues
            .SelectMany(queue => queue.StatusCounts)
            .GroupBy(count => count.Status)
            .OrderBy(group => group.Key)
            .Select(group => new DashboardStatusCount(group.Key, group.Sum(count => count.Count)))
            .ToList());
    }

    public IReadOnlyList<DashboardQueueDto> Queues { get; }

    public DashboardStatsDto Stats { get; }

    // Counts jobs of one queue (all queues when null) in the given statuses (all statuses when null).
    public long Count(string? queueName, IReadOnlyCollection<JobStatus>? statuses)
    {
        long total = 0;
        foreach (var queue in Queues)
        {
            if (queueName is not null && !string.Equals(queue.QueueName, queueName, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var count in queue.StatusCounts)
            {
                if (statuses is null || statuses.Contains(count.Status))
                {
                    total += count.Count;
                }
            }
        }

        return total;
    }
}
