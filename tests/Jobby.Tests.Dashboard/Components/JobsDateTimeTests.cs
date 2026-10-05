using Bunit;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client;
using Jobby.Dashboard.Client.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Jobby.Tests.Dashboard.Components;

public class JobsDateTimeTests : DashboardClientTestContext
{
    private static PagedResult<DashboardJobListItemDto> OneJob(DateTime createdUtc) => new(
        Items:
        [
            new DashboardJobListItemDto(Id: Guid.NewGuid(), JobName: "send-email", Status: JobStatus.Completed,
                QueueName: "emails",
                CreatedAt: createdUtc, ScheduledStartAt: createdUtc, LastStartedAt: null, LastFinishedAt: null,
                StartedCount: 1, ServerId: null, IsGroupLocker: false),
        ],
        TotalCount: 1, Page: 1, PageSize: 50);

    [Fact]
    public void Created_CellRendersInEffectiveTimezone()
    {
        SetupApi(req => req.RequestUri!.AbsolutePath switch
        {
            "/api/queues" => Array.Empty<DashboardQueueDto>(),
            _ => OneJob(new DateTime(2026, 6, 10, 13, 30, 0, DateTimeKind.Utc)),
        });
        var state = Services.GetRequiredService<DateTimeDisplayState>();
        state.ApplyForTests("Europe/London", DashboardDateStyle.DayFirstDot, DashboardTimeStyle.H24);

        var cut = Render<Jobs>();
        cut.WaitForAssertion(() => Assert.Contains("10.06.2026 14:30", cut.Markup));
    }
}