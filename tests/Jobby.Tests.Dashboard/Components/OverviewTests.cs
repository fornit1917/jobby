using Bunit;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.UI.Pages;

namespace Jobby.Tests.Dashboard.Components;

public class OverviewTests : DashboardClientTestContext
{
    private static readonly DashboardStatsDto NoJobs = new(StatusCounts: [], RecurrentStatusCounts: []);

    private void SetupOverview(DashboardStatsDto stats, params DashboardQueueDto[] queues) =>
        SetupOverview(stats, lockedGroups: 0, queues);

    private void SetupOverview(DashboardStatsDto stats, long lockedGroups, params DashboardQueueDto[] queues) =>
        SetupApi(req => req.RequestUri!.AbsolutePath switch
        {
            "/api/stats" => stats,
            "/api/servers" => Array.Empty<DashboardServerDto>(),
            "/api/queues" => queues,
            "/api/locked-groups/count" => new LockedGroupsCountDto(lockedGroups),
            _ => null,
        });

    [Fact]
    public void RecurrentTiles_ShowScheduledAndProcessingCountsApartFromJobTiles()
    {
        SetupOverview(new DashboardStatsDto(
            StatusCounts: [new DashboardStatusCount(JobStatus.Scheduled, 40)],
            RecurrentStatusCounts: [new DashboardStatusCount(JobStatus.Scheduled, 7)]));

        var cut = Render<Overview>();

        cut.WaitForAssertion(() =>
            Assert.Equal("7", cut.Find("[data-testid=recurrent-count-Scheduled]").TextContent));
        Assert.Equal("0", cut.Find("[data-testid=recurrent-count-Processing]").TextContent);
        Assert.Equal("40", cut.Find("[data-testid=count-Scheduled]").TextContent);
    }

    [Fact]
    public void QueuesWidget_ShowsBacklogAndTotalOfEachQueue()
    {
        SetupOverview(
            NoJobs,
            new DashboardQueueDto(QueueName: "emails", StatusCounts:
            [
                new DashboardStatusCount(JobStatus.Processing, 4),
                new DashboardStatusCount(JobStatus.Completed, 20),
            ]));

        var cut = Render<Overview>();

        cut.WaitForAssertion(() =>
            Assert.Equal("4", cut.Find("[data-testid=overview-queue-backlog-emails]").TextContent));
        Assert.Equal("24", cut.Find("[data-testid=overview-queue-total-emails]").TextContent);
    }

    [Fact]
    public void Cards_AreLinksToTheListsTheySummarize()
    {
        SetupOverview(
            new DashboardStatsDto(StatusCounts: [new DashboardStatusCount(JobStatus.Failed, 2)], RecurrentStatusCounts: []),
            lockedGroups: 1,
            new DashboardQueueDto(QueueName: "emails", StatusCounts: [new DashboardStatusCount(JobStatus.Failed, 2)]));

        var cut = Render<Overview>();

        cut.WaitForAssertion(() => Assert.Equal("jobs?status=Failed", cut.Find("a[data-testid=tile-Failed]").GetAttribute("href")));
        Assert.Equal("jobs?status=Failed", cut.Find("a[data-testid=overview-attention-failed]").GetAttribute("href"));
        Assert.Equal("locked-groups", cut.Find("a[data-testid=overview-attention-locked]").GetAttribute("href"));
        Assert.Equal("recurrent", cut.Find("a[data-testid=recurrent-tile-Scheduled]").GetAttribute("href"));
        Assert.Equal("recurrent", cut.Find("a[data-testid=recurrent-tile-Processing]").GetAttribute("href"));
        Assert.EndsWith("queue?name=emails", cut.Find("a[data-testid=overview-queue-emails]").GetAttribute("href"));
    }

    [Fact]
    public void NeedsAttention_ListsLockedGroupsFirstWithTheirCount()
    {
        SetupOverview(
            new DashboardStatsDto(StatusCounts: [new DashboardStatusCount(JobStatus.Failed, 2)], RecurrentStatusCounts: []),
            lockedGroups: 3);

        var cut = Render<Overview>();

        cut.WaitForAssertion(() => Assert.Equal(
            ["overview-attention-locked", "overview-attention-failed"],
            cut.FindAll("[data-testid=overview-attention] > a").Select(a => a.GetAttribute("data-testid"))));
        Assert.Equal("3 waiting for recovery",
            cut.Find("[data-testid=overview-attention-locked-count]").TextContent);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void NeedsAttention_IsEmptyOnlyWithoutLockedGroups(long lockedGroups, bool empty)
    {
        SetupOverview(NoJobs, lockedGroups);

        var cut = Render<Overview>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=overview-attention]")));
        Assert.Equal(empty, cut.FindAll("[data-testid=overview-attention-empty]").Count == 1);
        Assert.Equal(!empty, cut.FindAll("[data-testid=overview-attention-locked]").Count == 1);
    }
}
