using Bunit;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.UI.Pages;

namespace Jobby.Tests.Dashboard.Components;

public class JobDetailTests : DashboardClientTestContext
{
    private static DashboardJobDetailsDto Job(bool isGroupLocker) => new(
        Id: Guid.NewGuid(), JobName: "SyncOrder", Status: JobStatus.Failed, QueueName: "default",
        CreatedAt: DateTime.UtcNow, ScheduledStartAt: DateTime.UtcNow,
        LastStartedAt: null, LastFinishedAt: null, StartedCount: 1, ServerId: null,
        JobParam: null, Error: null, Schedule: null, SchedulerType: null, NextJobId: null,
        SerializableGroupId: isGroupLocker ? "g1" : null, LockGroupIfFailed: false,
        IsExclusive: false, CanBeRestarted: false, IsGroupLocker: isGroupLocker);

    [Fact]
    public void Locker_ShowsRecoveryBannerWithGroupLink()
    {
        SetupApi(req => req.RequestUri!.AbsolutePath.StartsWith("/api/jobs/") ? Job(isGroupLocker: true) : null);

        var cut = Render<JobDetail>(p => p.Add(x => x.Id, Guid.NewGuid()));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=detail-locker-note]")));
        Assert.Contains("locked-groups/detail?groupId=g1", cut.Markup);
    }

    [Fact]
    public void NonLocker_HasNoRecoveryBanner()
    {
        SetupApi(req => req.RequestUri!.AbsolutePath.StartsWith("/api/jobs/") ? Job(isGroupLocker: false) : null);

        var cut = Render<JobDetail>(p => p.Add(x => x.Id, Guid.NewGuid()));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=detail-table]")));
        Assert.Empty(cut.FindAll("[data-testid=detail-locker-note]"));
    }

    [Theory]
    [InlineData(true, "Yes")]
    [InlineData(false, "No")]
    public void Recurrent_ShowsExclusiveStateAndMeaning(bool isExclusive, string expected)
    {
        var job = Job(isGroupLocker: false) with { Schedule = "0 * * * *", IsExclusive = isExclusive };
        SetupApi(req => req.RequestUri!.AbsolutePath.StartsWith("/api/jobs/") ? job : null);

        var cut = Render<JobDetail>(p => p.Add(x => x.Id, job.Id));

        cut.WaitForAssertion(() =>
        {
            var row = cut.Find("[data-testid=detail-exclusive]");
            Assert.Equal(expected, row.QuerySelector("td > span")!.TextContent);
            Assert.Contains("does not prevent concurrent execution", row.TextContent);
        });
    }
}
