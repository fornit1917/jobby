using Bunit;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client.Pages;

namespace Jobby.Tests.Dashboard.Components;

public class QueuesTests : DashboardClientTestContext
{
    private static List<DashboardQueueDto> OneQueue() =>
        [new(QueueName: "emails", StatusCounts: [new DashboardStatusCount(JobStatus.Scheduled, 3)])];

    private static PagedResult<DashboardJobListItemDto> EmptyJobs() =>
        new(Items: [], TotalCount: 0, Page: 1, PageSize: 5);

    [Fact]
    public void Preview_IsNotFetchedUntilTheRowIsExpanded()
    {
        SetupApi(req => req.RequestUri!.AbsolutePath switch
        {
            "/api/queues" => OneQueue(),
            "/api/jobs" => EmptyJobs(),
            _ => null,
        });

        var cut = Render<Queues>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=queues-table]")));
        Assert.Empty(Handler.RequestsTo("/api/jobs"));

        cut.Find("[data-testid=queue-toggle-emails]").Click();

        cut.WaitForAssertion(() => Assert.Single(Handler.RequestsTo("/api/jobs")));
        var query = Uri.UnescapeDataString(Handler.RequestsTo("/api/jobs").Single().RequestUri!.Query);
        Assert.Contains("queueName=emails", query);
        Assert.Contains("statuses=Scheduled,Processing,WaitingPrev,Frozen", query);
        Assert.Contains("pageSize=5", query);
        Assert.Contains("sortBy=ScheduledStartAt", query);
    }

    [Fact]
    public void Expanding_AnEmptyQueueShowsNoJobsMessage()
    {
        SetupApi(req => req.RequestUri!.AbsolutePath switch
        {
            "/api/queues" => OneQueue(),
            "/api/jobs" => EmptyJobs(),
            _ => null,
        });

        var cut = Render<Queues>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=queues-table]")));
        cut.Find("[data-testid=queue-toggle-emails]").Click();

        cut.WaitForAssertion(() => Assert.Contains("No jobs queued.", cut.Markup));
    }

    [Fact]
    public void Refreshing_ReloadsThePreviewForExpandedRows()
    {
        SetupApi(req => req.RequestUri!.AbsolutePath switch
        {
            "/api/queues" => OneQueue(),
            "/api/jobs" => OneBacklogJob(),
            _ => null,
        });

        var cut = Render<Queues>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=queues-table]")));
        cut.Find("[data-testid=queue-toggle-emails]").Click();
        cut.WaitForAssertion(() => Assert.Single(Handler.RequestsTo("/api/jobs")));

        var before = Handler.RequestsTo("/api/jobs").Count;
        cut.Find("[data-testid=refresh-now]").Click();

        cut.WaitForAssertion(() => Assert.True(Handler.RequestsTo("/api/jobs").Count > before));
        Assert.NotEmpty(cut.FindAll("[data-testid=queue-preview-emails]"));
    }

    private static PagedResult<DashboardJobListItemDto> OneBacklogJob() => new(
        Items:
        [
            new DashboardJobListItemDto(
                Id: Guid.NewGuid(), JobName: "send-email", Status: JobStatus.Scheduled,
                QueueName: "emails", CreatedAt: DateTime.UtcNow, ScheduledStartAt: DateTime.UtcNow,
                LastStartedAt: null, LastFinishedAt: null, StartedCount: 0, ServerId: null, IsGroupLocker: false),
        ],
        TotalCount: 1, Page: 1, PageSize: 5);
}