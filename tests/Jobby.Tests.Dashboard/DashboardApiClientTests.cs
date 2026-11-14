using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.UI;
using Jobby.Tests.Dashboard.Components;

namespace Jobby.Tests.Dashboard;

public class DashboardApiClientTests
{
    [Fact]
    public async Task GetJobsAsync_SerializesStatusesAsCsv()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            new PagedResult<DashboardJobListItemDto>(Items: [], TotalCount: 0, Page: 1, PageSize: 50));
        using (var http = new HttpClient(handler))
        {
            http.BaseAddress = new Uri("http://localhost/");
            var client = new DashboardApiClient(http, new TestNavigationManager(), new DashboardClientConfig());

            await client.GetJobsAsync(new DashboardJobsQuery
            {
                QueueName = "emails",
                Statuses = [JobStatus.Scheduled, JobStatus.Processing, JobStatus.WaitingPrev, JobStatus.Frozen],
            }, TestContext.Current.CancellationToken);

            var query = Uri.UnescapeDataString(handler.Requests.Single().RequestUri!.Query);
            Assert.Contains("queueName=emails", query);
            Assert.Contains("statuses=Scheduled,Processing,WaitingPrev,Frozen", query);
            Assert.Contains("sortBy=ScheduledStartAt", query);
            Assert.Contains("sortDescending=False", query);
        }
    }

    [Fact]
    public async Task GetJobsAsync_SerializesScheduledBoundsAndExplicitSort()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            new PagedResult<DashboardJobListItemDto>(Items: [], TotalCount: 0, Page: 1, PageSize: 50));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new DashboardApiClient(http, new TestNavigationManager(), new DashboardClientConfig());
        var from = new DateTime(2026, 6, 10, 12, 30, 0, DateTimeKind.Utc);
        var to = from.AddDays(1).AddTicks(-1);

        await client.GetJobsAsync(new DashboardJobsQuery
        {
            ScheduledFrom = from,
            ScheduledTo = to,
            SortBy = DashboardJobsSortBy.CreatedAt,
            SortDescending = true,
        }, TestContext.Current.CancellationToken);

        var query = Uri.UnescapeDataString(handler.Requests.Single().RequestUri!.Query);
        Assert.Contains("scheduledFrom=2026-06-10T12:30:00.0000000Z", query);
        Assert.Contains("scheduledTo=2026-06-11T12:29:59.9999999Z", query);
        Assert.Contains("sortBy=CreatedAt", query);
        Assert.Contains("sortDescending=True", query);
    }

    [Fact]
    public async Task GetLockedGroupsAsync_ForwardsPaging()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            new PagedResult<LockedGroupDto>(Items: [], TotalCount: 0, Page: 1, PageSize: 50));
        using (var http = new HttpClient(handler))
        {
            http.BaseAddress = new Uri("http://localhost/");
            var client = new DashboardApiClient(http, new TestNavigationManager(), new DashboardClientConfig());

            await client.GetLockedGroupsAsync(new LockedGroupsQuery { Page = 3, PageSize = 25 });

            var query = Uri.UnescapeDataString(handler.RequestsTo("/api/locked-groups").Single().RequestUri!.Query);
            Assert.Contains("page=3", query);
            Assert.Contains("pageSize=25", query);
        }
    }

    [Fact]
    public async Task RequestGroupUnlockAsync_PostsToUnlockEndpoint_WithManagementHeader_AndParsesResult()
    {
        var handler = new RecordingHttpMessageHandler(req => req.RequestUri!.AbsolutePath switch
        {
            "/api/config" => new JobbyDashboardClientConfigDto
            {
                ManagementEnabled = true,
                ManagementRequestHeaderName = "X-Jobby-Dashboard",
                ManagementRequestToken = "tok",
            },
            "/api/locked-groups/unlock-request" => new RequestGroupUnlockResultDto(true),
            _ => null,
        });
        using (var http = new HttpClient(handler))
        {
            http.BaseAddress = new Uri("http://localhost/");
            var config = new DashboardClientConfig();
            await config.LoadAsync(http);
            var client = new DashboardApiClient(http, new TestNavigationManager(), config);

            var result = await client.RequestGroupUnlockAsync("group-1");

            Assert.True(result.Inserted);
            var post = handler.RequestsTo("/api/locked-groups/unlock-request").Single();
            Assert.Equal(HttpMethod.Post, post.Method);
            Assert.True(post.Headers.Contains("X-Jobby-Dashboard"));
        }
    }
}