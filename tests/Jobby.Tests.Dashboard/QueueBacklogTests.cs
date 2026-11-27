using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.UI.Shared;

namespace Jobby.Tests.Dashboard;

public class QueueBacklogTests
{
    [Fact]
    public void Count_SumsEveryStatusExceptCompletedAndFailed()
    {
        var counts = Enum.GetValues<JobStatus>()
            .Select(status => new DashboardStatusCount(status, Count: 1L << (int)status))
            .ToList();
        var finished = counts
            .Where(c => c.Status is JobStatus.Completed or JobStatus.Failed)
            .Sum(c => c.Count);

        Assert.Equal(counts.Sum(c => c.Count) - finished, QueueBacklog.Count(counts));
    }
}
