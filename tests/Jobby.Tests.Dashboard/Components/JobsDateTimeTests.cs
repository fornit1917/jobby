using Bunit;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.UI;
using Jobby.Dashboard.UI.Localization;
using Jobby.Dashboard.UI.Pages;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Jobby.Tests.Dashboard.Components;

public class JobsDateTimeTests : DashboardClientTestContext
{
    private static PagedResult<DashboardJobListItemDto> OneJob(DateTime createdUtc, DateTime? scheduledUtc = null) => new(
        Items:
        [
            new DashboardJobListItemDto(Id: Guid.NewGuid(), JobName: "send-email", Status: JobStatus.Completed,
                QueueName: "emails",
                CreatedAt: createdUtc, ScheduledStartAt: scheduledUtc ?? createdUtc, LastStartedAt: null, LastFinishedAt: null,
                StartedCount: 1, ServerId: null, IsGroupLocker: false),
        ],
        TotalCount: 1, Page: 1, PageSize: 50);

    private void SetupJobs() => SetupApi(req => req.RequestUri!.AbsolutePath switch
    {
        "/api/queues" => Array.Empty<DashboardQueueDto>(),
        _ => OneJob(new DateTime(2026, 6, 10, 13, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc)),
    });

    private string LatestJobsQuery =>
        Uri.UnescapeDataString(Handler.RequestsTo("/api/jobs").Last().RequestUri!.Query);

    [Fact]
    public void CreatedAndScheduled_CellsRenderInEffectiveTimezone()
    {
        SetupJobs();
        var state = Services.GetRequiredService<DateTimeDisplayState>();
        state.ApplyForTests("Europe/London", DashboardDateStyle.DayFirstDot, DashboardTimeStyle.H24);

        var cut = Render<Jobs>();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("10.06.2026 14:30", cut.Markup);
            Assert.Contains("10.06.2026 13:00", cut.Markup);
        });
    }

    [Fact]
    public async Task ScheduledColumn_StartsAscending_AndEmptySortUsesTheSameDefault()
    {
        SetupJobs();
        var cut = Render<Jobs>();
        cut.WaitForAssertion(() => Assert.NotEmpty(Handler.RequestsTo("/api/jobs")));
        var grid = cut.FindComponent<MudDataGrid<DashboardJobListItemDto>>().Instance;
        var sort = Assert.Single(grid.SortDefinitions);
        Assert.Equal(nameof(DashboardJobListItemDto.ScheduledStartAt), sort.Key);
        Assert.False(sort.Value.Descending);
        Assert.Contains("sortBy=ScheduledStartAt", LatestJobsQuery);
        Assert.Contains("sortDescending=False", LatestJobsQuery);
        Assert.Contains("Scheduled from", cut.Markup);
        Assert.Contains("Scheduled to", cut.Markup);

        await cut.InvokeAsync(async () =>
        {
            grid.SortDefinitions.Clear();
            await grid.ReloadServerData();
        });

        Assert.Contains("sortBy=ScheduledStartAt", LatestJobsQuery);
        Assert.Contains("sortDescending=False", LatestJobsQuery);
    }

    [Fact]
    public async Task ScheduledDates_MapToInclusiveDaysInTheEffectiveTimezone()
    {
        SetupJobs();
        Services.GetRequiredService<DateTimeDisplayState>()
            .ApplyForTests("Europe/London", DashboardDateStyle.Iso, DashboardTimeStyle.H24);
        var cut = Render<Jobs>();
        cut.WaitForAssertion(() => Assert.NotEmpty(Handler.RequestsTo("/api/jobs")));
        var pickers = cut.FindComponents<MudDatePicker>();
        var date = new DateTime(2026, 6, 10);

        await cut.InvokeAsync(() => pickers[0].Instance.DateChanged.InvokeAsync(date));
        await cut.InvokeAsync(() => pickers[1].Instance.DateChanged.InvokeAsync(date));

        Assert.Contains("scheduledFrom=2026-06-09T23:00:00.0000000Z", LatestJobsQuery);
        Assert.Contains("scheduledTo=2026-06-10T22:59:59.9999999Z", LatestJobsQuery);
    }

    [Theory]
    [InlineData(DashboardLanguage.English, "The start date must not be after the end date.")]
    [InlineData(DashboardLanguage.Russian, "Начальная дата не должна быть позже конечной.")]
    [InlineData(DashboardLanguage.PortugueseBrazil, "A data inicial não pode ser posterior à data final.")]
    public async Task ReversedScheduledDates_ShowLocalizedError_KeepResults_AndRecover(
        DashboardLanguage language, string message)
    {
        SetupJobs();
        Services.GetRequiredService<LanguageState>().ApplyForTests(language);
        var cut = Render<Jobs>();
        cut.WaitForAssertion(() => Assert.Contains("send-email", cut.Markup));
        var pickers = cut.FindComponents<MudDatePicker>();
        var from = new DateTime(2026, 6, 10);
        await cut.InvokeAsync(() => pickers[0].Instance.DateChanged.InvokeAsync(from));
        var validRequestCount = Handler.RequestsTo("/api/jobs").Count;

        await cut.InvokeAsync(() => pickers[1].Instance.DateChanged.InvokeAsync(from.AddDays(-1)));
        await cut.InvokeAsync(() => cut.FindComponent<MudDataGrid<DashboardJobListItemDto>>().Instance.ReloadServerData());

        Assert.Equal(message, cut.Find("[data-testid=jobs-range-error]").TextContent.Trim());
        Assert.Equal(validRequestCount, Handler.RequestsTo("/api/jobs").Count);
        Assert.Contains("send-email", cut.Markup);

        await cut.InvokeAsync(() => pickers[1].Instance.DateChanged.InvokeAsync(from));

        Assert.Empty(cut.FindAll("[data-testid=jobs-range-error]"));
        // Directly invoking DateChanged can echo the callback when MudDatePicker receives its updated Date parameter.
        var correctedRequests = Handler.RequestsTo("/api/jobs").Skip(validRequestCount).ToArray();
        Assert.NotEmpty(correctedRequests);
        Assert.All(correctedRequests, request =>
            Assert.Contains("scheduledTo=2026-06-10T23:59:59.9999999Z",
                Uri.UnescapeDataString(request.RequestUri!.Query)));
    }

    [Theory]
    [InlineData(nameof(DashboardJobListItemDto.CreatedAt))]
    [InlineData(nameof(DashboardJobListItemDto.ScheduledStartAt))]
    public async Task ManualSort_IsPreservedWhenFilteringAndRefreshing(string property)
    {
        SetupJobs();
        var cut = Render<Jobs>();
        cut.WaitForAssertion(() => Assert.NotEmpty(Handler.RequestsTo("/api/jobs")));
        var grid = cut.FindComponent<MudDataGrid<DashboardJobListItemDto>>().Instance;
        await cut.InvokeAsync(() => grid.SetSortAsync(property, SortDirection.Descending,
            item => property == nameof(DashboardJobListItemDto.CreatedAt) ? item.CreatedAt : item.ScheduledStartAt));

        await cut.InvokeAsync(() => cut.FindComponents<MudDatePicker>()[0].Instance.DateChanged
            .InvokeAsync(new DateTime(2026, 6, 10)));
        Assert.Contains($"sortBy={property}", LatestJobsQuery);
        Assert.Contains("sortDescending=True", LatestJobsQuery);

        await cut.InvokeAsync(() => grid.ReloadServerData());
        Assert.Contains($"sortBy={property}", LatestJobsQuery);
        Assert.Contains("sortDescending=True", LatestJobsQuery);
    }
}
