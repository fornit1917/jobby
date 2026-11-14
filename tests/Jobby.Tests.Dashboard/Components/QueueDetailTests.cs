using Bunit;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.UI;
using Jobby.Dashboard.UI.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Jobby.Tests.Dashboard.Components;

public class QueueDetailTests : DashboardClientTestContext
{
    private static PagedResult<DashboardJobListItemDto> EmptyJobs() =>
        new(Items: [], TotalCount: 0, Page: 1, PageSize: 10);

    private void NavigateToQueue(string name) =>
        Services.GetRequiredService<NavigationManager>().NavigateTo($"queue?name={name}");

    [Fact]
    public void Backlog_IsTheDefaultView()
    {
        SetupApi(_ => EmptyJobs());
        NavigateToQueue("emails");
        var cut = Render<QueueDetail>();

        cut.WaitForAssertion(() => Assert.NotEmpty(Handler.RequestsTo("/api/jobs")));
        var query = Uri.UnescapeDataString(Handler.RequestsTo("/api/jobs")[Handler.RequestsTo("/api/jobs").Count - 1]
            .RequestUri!.Query);
        Assert.Contains("queueName=emails", query);
        Assert.Contains("statuses=Scheduled,Processing,WaitingPrev,Frozen", query);
        Assert.Contains("sortBy=ScheduledStartAt", query);
        Assert.Contains("sortDescending=False", query);
        var sort = Assert.Single(cut.FindComponent<MudDataGrid<DashboardJobListItemDto>>().Instance.SortDefinitions);
        Assert.Equal(nameof(DashboardJobListItemDto.ScheduledStartAt), sort.Key);
        Assert.False(sort.Value.Descending);
    }

    [Fact]
    public void Switching_ToAllRemovesTheStatusFilterAndResetsSort()
    {
        SetupApi(_ => EmptyJobs());
        NavigateToQueue("emails");
        var cut = Render<QueueDetail>();
        cut.WaitForAssertion(() => Assert.NotEmpty(Handler.RequestsTo("/api/jobs")));

        cut.Find("[data-testid=queue-mode-all]").Click();

        cut.WaitForAssertion(() =>
        {
            var query = Uri.UnescapeDataString(Handler.RequestsTo("/api/jobs").Last().RequestUri!.Query);
            Assert.DoesNotContain("statuses=", query);
            Assert.Contains("sortBy=ScheduledStartAt", query);
            Assert.Contains("sortDescending=False", query);
            Assert.Contains("page=1", query);
            var sort = Assert.Single(cut.FindComponent<MudDataGrid<DashboardJobListItemDto>>().Instance.SortDefinitions);
            Assert.Equal(nameof(DashboardJobListItemDto.ScheduledStartAt), sort.Key);
            Assert.False(sort.Value.Descending);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManualSort_SurvivesRefresh_AndEmptySortUsesScheduledAscending(bool allJobs)
    {
        SetupApi(_ => EmptyJobs());
        NavigateToQueue("emails");
        var cut = Render<QueueDetail>();
        cut.WaitForAssertion(() => Assert.NotEmpty(Handler.RequestsTo("/api/jobs")));
        if (allJobs)
        {
            cut.Find("[data-testid=queue-mode-all]").Click();
            cut.WaitForAssertion(() => Assert.DoesNotContain("statuses=",
                Uri.UnescapeDataString(Handler.RequestsTo("/api/jobs").Last().RequestUri!.Query)));
        }

        var grid = cut.FindComponent<MudDataGrid<DashboardJobListItemDto>>().Instance;
        await cut.InvokeAsync(() => grid.SetSortAsync(nameof(DashboardJobListItemDto.JobName),
            SortDirection.Descending, item => item.JobName));
        await cut.InvokeAsync(() => grid.ReloadServerData());

        var query = Uri.UnescapeDataString(Handler.RequestsTo("/api/jobs").Last().RequestUri!.Query);
        Assert.Contains("sortBy=JobName", query);
        Assert.Contains("sortDescending=True", query);

        await cut.InvokeAsync(async () =>
        {
            grid.SortDefinitions.Clear();
            await grid.ReloadServerData();
        });

        query = Uri.UnescapeDataString(Handler.RequestsTo("/api/jobs").Last().RequestUri!.Query);
        Assert.Contains("sortBy=ScheduledStartAt", query);
        Assert.Contains("sortDescending=False", query);
    }

    [Fact]
    public void Scheduled_CellReformatsWhenTimezoneChanges()
    {
        SetupApi(req => req.RequestUri!.AbsolutePath switch
        {
            "/api/jobs" => new PagedResult<DashboardJobListItemDto>(
                Items:
                [
                    new DashboardJobListItemDto(Id: Guid.NewGuid(), JobName: "j", Status: JobStatus.Scheduled,
                        QueueName: "emails",
                        CreatedAt: new DateTime(2026, 6, 10, 13, 30, 0, DateTimeKind.Utc),
                        ScheduledStartAt: new DateTime(2026, 6, 10, 13, 30, 0, DateTimeKind.Utc),
                        LastStartedAt: null, LastFinishedAt: null, StartedCount: 0, ServerId: null,
                        IsGroupLocker: false),
                ],
                TotalCount: 1, Page: 1, PageSize: 50),
            _ => null,
        });
        var state = Services.GetRequiredService<DateTimeDisplayState>();

        NavigateToQueue("emails");
        var cut = Render<QueueDetail>();
        cut.WaitForAssertion(() => Assert.Contains("2026-06-10 13:30:00", cut.Markup));

        state.ApplyForTests("Europe/London", DashboardDateStyle.Iso, DashboardTimeStyle.H24);
        cut.InvokeAsync(state.RaiseChangedForTests);
        cut.WaitForAssertion(() => Assert.Contains("2026-06-10 14:30", cut.Markup));
    }
}