using Bunit;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.UI.Pages;

namespace Jobby.Tests.Dashboard.Components;

public class OverviewTests : DashboardClientTestContext
{
    private void SetupOverview(DashboardStatsDto stats, params DashboardQueueDto[] queues) =>
        SetupApi(req => req.RequestUri!.AbsolutePath switch
        {
            "/api/stats" => stats,
            "/api/servers" => Array.Empty<DashboardServerDto>(),
            "/api/queues" => queues,
            _ => null,
        });

    [Fact]
    public void RecurrentWidget_ShowsScheduledAndProcessingCountsApartFromStatusTiles()
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
            new DashboardStatsDto(StatusCounts: [], RecurrentStatusCounts: []),
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
}
