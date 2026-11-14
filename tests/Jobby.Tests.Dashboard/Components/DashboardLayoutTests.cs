using Bunit;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.UI.Layout;

namespace Jobby.Tests.Dashboard.Components;

public class DashboardLayoutTests : DashboardClientTestContext
{
    [Fact]
    public void LockedGroupsBadge_ShowsCountFromCountEndpoint_WithoutLoadingAGroupsPage()
    {
        SetupApi(req => req.RequestUri!.AbsolutePath == "/api/locked-groups/count"
            ? new LockedGroupsCountDto(3)
            : null);

        var cut = Render<DashboardLayout>();

        cut.WaitForAssertion(() =>
            Assert.Equal("3", cut.Find("[data-testid=locked-count-badge]").TextContent));
        Assert.Empty(Handler.RequestsTo("/api/locked-groups"));
    }

    [Fact]
    public void Toggle_FlipsDrawerOpenStateAndBack()
    {
        SetupApi(req => req.RequestUri!.AbsolutePath == "/api/locked-groups/count"
            ? new LockedGroupsCountDto(0)
            : null);

        var cut = Render<DashboardLayout>();

        var initiallyClosed = cut.Find("aside.mud-drawer").ClassName!.Contains("mud-drawer--closed");

        cut.Find("[data-testid=drawer-toggle]").Click();
        Assert.NotEqual(initiallyClosed,
            cut.Find("aside.mud-drawer").ClassName!.Contains("mud-drawer--closed"));

        cut.Find("[data-testid=drawer-toggle]").Click();
        Assert.Equal(initiallyClosed,
            cut.Find("aside.mud-drawer").ClassName!.Contains("mud-drawer--closed"));
    }
}