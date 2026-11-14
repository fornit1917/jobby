using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Postgres.Dashboard;

namespace Jobby.Tests.Postgres.Dashboard;

public class QueueStatusCountsTests
{
    private static readonly QueueStatusCounts Counts = new(
    [
        new DashboardQueueDto("default",
        [
            new DashboardStatusCount(JobStatus.Scheduled, 10),
            new DashboardStatusCount(JobStatus.Failed, 2),
        ]),
        new DashboardQueueDto("emails",
        [
            new DashboardStatusCount(JobStatus.Scheduled, 5),
            new DashboardStatusCount(JobStatus.Processing, 1),
        ]),
    ]);

    [Fact]
    public void Count_WithoutFilters_SumsAllQueuesAndStatuses()
    {
        Assert.Equal(18, Counts.Count(queueName: null, statuses: null));
    }

    [Fact]
    public void Count_ByQueue_SumsOnlyThatQueue()
    {
        Assert.Equal(6, Counts.Count("emails", statuses: null));
    }

    [Fact]
    public void Count_ByStatuses_SumsThoseStatusesAcrossQueues()
    {
        Assert.Equal(16, Counts.Count(queueName: null, [JobStatus.Scheduled, JobStatus.Processing]));
    }

    [Fact]
    public void Count_ByQueueAndStatuses_AppliesBothFilters()
    {
        Assert.Equal(2, Counts.Count("default", [JobStatus.Failed, JobStatus.Processing]));
    }

    [Fact]
    public void Count_QueueNameIsMatchedExactly_LikeTheSqlFilter()
    {
        Assert.Equal(0, Counts.Count("Default", statuses: null));
        Assert.Equal(0, Counts.Count("missing", statuses: null));
    }

    [Fact]
    public void Stats_SumEachStatusAcrossQueues()
    {
        Assert.Equal(
            [
                new DashboardStatusCount(JobStatus.Scheduled, 15),
                new DashboardStatusCount(JobStatus.Processing, 1),
                new DashboardStatusCount(JobStatus.Failed, 2),
            ],
            Counts.Stats.StatusCounts);
    }
}
