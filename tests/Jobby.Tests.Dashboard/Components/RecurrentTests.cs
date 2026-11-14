using Bunit;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.UI.Pages;

namespace Jobby.Tests.Dashboard.Components;

public class RecurrentTests : DashboardClientTestContext
{
    [Fact]
    public void List_ShowsBothExclusiveStates()
    {
        var nextRun = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);
        var exclusive = new DashboardRecurrentJobDto(
            Guid.NewGuid(), "Nightly", "0 0 * * *", null, "default", JobStatus.Scheduled,
            nextRun, null, null, null, true);
        var nonexclusive = exclusive with { Id = Guid.NewGuid(), JobName = "Hourly", IsExclusive = false };
        SetupApi(req => req.RequestUri!.AbsolutePath == "/api/recurrent"
            ? new PagedResult<DashboardRecurrentJobDto>([exclusive, nonexclusive], 2, 1, 50)
            : null);

        var cut = Render<Recurrent>();

        cut.WaitForAssertion(() => Assert.Equal(
            new[] { "Yes", "No" },
            cut.FindAll("[data-testid=recurrent-exclusive]").Select(cell => cell.TextContent)));
        Assert.Contains("Exclusive", cut.Find("thead").TextContent);
    }
}
