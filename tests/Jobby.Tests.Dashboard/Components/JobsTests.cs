using System.Net;
using Bunit;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.UI.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Jobby.Tests.Dashboard.Components;

public class JobsTests : DashboardClientTestContext
{
    private static PagedResult<DashboardJobListItemDto> OneJob() => new(
        Items:
        [
            new DashboardJobListItemDto(Id: Guid.NewGuid(), JobName: "send-email", Status: JobStatus.Failed,
                QueueName: "emails", CreatedAt: DateTime.UtcNow, ScheduledStartAt: DateTime.UtcNow,
                LastStartedAt: null, LastFinishedAt: null, StartedCount: 1, ServerId: null, IsGroupLocker: false),
        ],
        TotalCount: 1, Page: 1, PageSize: 10);

    private void SetupJobs() => SetupApi(req => req.RequestUri!.AbsolutePath == "/api/queues"
        ? Array.Empty<DashboardQueueDto>()
        : OneJob());

    private void NavigateTo(string uri) => Services.GetRequiredService<NavigationManager>().NavigateTo(uri);

    private IReadOnlyList<string> JobsQueries =>
        [.. Handler.RequestsTo("/api/jobs").Select(request => Uri.UnescapeDataString(request.RequestUri!.Query))];

    [Fact]
    public void StatusRemovedFromTheUrl_ClearsTheFilter()
    {
        SetupJobs();
        NavigateTo("jobs?status=Failed");
        var cut = Render<Jobs>();
        cut.WaitForAssertion(() => Assert.Contains("status=Failed", JobsQueries.Last()));

        NavigateTo("jobs");

        cut.WaitForAssertion(() => Assert.DoesNotContain("status=", JobsQueries.Last()));
    }

    [Fact]
    public void FailedQueueListRequest_LeavesTheJobsUsable()
    {
        SetupApi(req => req.RequestUri!.AbsolutePath == "/api/queues"
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : OneJob());

        var cut = Render<Jobs>();

        cut.WaitForAssertion(() => Assert.Contains("send-email", cut.Markup));
        Assert.Empty(cut.FindAll("[data-testid=jobs-error]"));
    }
}
