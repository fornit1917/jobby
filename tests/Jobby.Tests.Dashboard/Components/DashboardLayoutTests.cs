using Bunit;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client.Layout;

namespace Jobby.Tests.Dashboard.Components;

public class DashboardLayoutTests : DashboardClientTestContext
{
    [Fact]
    public void Toggle_FlipsDrawerOpenStateAndBack()
    {
        SetupApi(req => req.RequestUri!.AbsolutePath == "/api/locked-groups"
            ? new PagedResult<LockedGroupDto>(Items: [], TotalCount: 0, Page: 1, PageSize: 1)
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