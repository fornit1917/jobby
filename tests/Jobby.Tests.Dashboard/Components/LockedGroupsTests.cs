using Bunit;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client.Pages;
using MudBlazor;

namespace Jobby.Tests.Dashboard.Components;

public class LockedGroupsTests : DashboardClientTestContext
{
    private static LockedGroupDto Group(string id, bool requested = false) => new(
        GroupId: id, Queues: ["default"], LockerId: Guid.NewGuid(), LockerName: "SyncOrder",
        LockerStatus: JobStatus.Failed, LockerServerId: null, LockerLastStartedAt: null, LockerLastFinishedAt: null,
        LockerCreatedAt: DateTime.UtcNow, LockerError: null, StuckSince: DateTime.UtcNow.AddHours(-1),
        FrozenCount: 2, ScheduledCount: 0, WaitingCount: 0, NonLockerFailedCount: 0, OldestFrozenAt: null,
        UnlockRequested: requested, UnlockRequestedAt: requested ? DateTime.UtcNow : null);

    private static JobbyDashboardClientConfigDto Config(bool managementEnabled) => new()
    {
        RefreshIntervalSeconds = null,
        ManagementEnabled = managementEnabled,
        ManagementRequestHeaderName = "X-Jobby-Dashboard",
        ManagementRequestToken = "tok",
    };

    private static PagedResult<LockedGroupDto> Page(params LockedGroupDto[] groups) =>
        new(Items: groups, TotalCount: groups.Length, Page: 1, PageSize: 50);

    [Fact]
    public async Task Renders_RowsAndUnlockButtonWhenManagementEnabledAndNotRequested()
    {
        await SetupApiAndLoadConfigAsync(req => req.RequestUri!.AbsolutePath switch
        {
            "/api/config" => Config(managementEnabled: true),
            "/api/locked-groups" => Page(Group("order-4521")),
            _ => null,
        });

        var cut = Render<LockedGroups>();

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("[data-testid=locked-groups-table]")));
        Assert.NotEmpty(cut.FindAll("[data-testid=group-unlock-order-4521]"));
    }

    [Fact]
    public async Task Shows_RequestedTextAndNoButtonWhenAlreadyRequested()
    {
        await SetupApiAndLoadConfigAsync(req => req.RequestUri!.AbsolutePath switch
        {
            "/api/config" => Config(managementEnabled: true),
            "/api/locked-groups" => Page(Group("order-4521", requested: true)),
            _ => null,
        });

        var cut = Render<LockedGroups>();

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("[data-testid=group-requested-order-4521]")));
        Assert.Empty(cut.FindAll("[data-testid=group-unlock-order-4521]"));
    }

    [Fact]
    public async Task Hides_ButtonWhenManagementDisabled()
    {
        await SetupApiAndLoadConfigAsync(req => req.RequestUri!.AbsolutePath switch
        {
            "/api/config" => Config(managementEnabled: false),
            "/api/locked-groups" => Page(Group("order-4521")),
            _ => null,
        });

        var cut = Render<LockedGroups>();

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("[data-testid=locked-groups-table]")));
        Assert.Empty(cut.FindAll("[data-testid=group-unlock-order-4521]"));
    }

    [Fact]
    public async Task Empty_StateWhenNoLockedGroups()
    {
        await SetupApiAndLoadConfigAsync(req => req.RequestUri!.AbsolutePath switch
        {
            "/api/config" => Config(managementEnabled: true),
            "/api/locked-groups" => Page(),
            _ => null,
        });

        var cut = Render<LockedGroups>();

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("[data-testid=locked-groups-empty]")));
    }

    [Fact]
    public async Task Pager_LoadsTheNextPage()
    {
        await SetupApiAndLoadConfigAsync(req => req.RequestUri!.AbsolutePath switch
        {
            "/api/config" => Config(managementEnabled: true),
            "/api/locked-groups" => req.RequestUri!.Query.Contains("page=2")
                ? new PagedResult<LockedGroupDto>(Items: [Group("page2-grp")], TotalCount: 60, Page: 2, PageSize: 50)
                : new PagedResult<LockedGroupDto>(Items: [Group("page1-grp")], TotalCount: 60, Page: 1, PageSize: 50),
            _ => null,
        });

        var cut = Render<LockedGroups>();
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("[data-testid=group-row-page1-grp]")));

        await cut.Find("[data-testid=locked-groups-next]").ClickAsync();

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("[data-testid=group-row-page2-grp]")));
    }

    [Fact]
    public async Task Unlock_RequiresConfirmationThenPostsAndReloads()
    {
        await SetupApiAndLoadConfigAsync(req => req.RequestUri!.AbsolutePath switch
        {
            "/api/config" => Config(managementEnabled: true),
            "/api/locked-groups" => Page(Group("order-4521")),
            "/api/locked-groups/unlock-request" => new RequestGroupUnlockResultDto(true),
            _ => null,
        });

        var cut = Render(b =>
        {
            b.OpenComponent<MudDialogProvider>(0);
            b.CloseComponent();
            b.OpenComponent<LockedGroups>(1);
            b.CloseComponent();
        });

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("[data-testid=group-unlock-order-4521]")));
        var loadsBefore = Handler.RequestsTo("/api/locked-groups").Count;

        var unlockClick = cut.Find("[data-testid=group-unlock-order-4521]").ClickAsync();

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("div.mud-dialog")));
        Assert.Empty(Handler.RequestsTo("/api/locked-groups/unlock-request"));

        var confirm = cut.FindAll("div.mud-dialog button").First(x => x.TextContent.Contains("Request Unlock"));
        await confirm.ClickAsync();
        await unlockClick;

        await cut.WaitForAssertionAsync(() => Assert.Single(Handler.RequestsTo("/api/locked-groups/unlock-request")));
        await cut.WaitForAssertionAsync(() =>
            Assert.True(Handler.RequestsTo("/api/locked-groups").Count > loadsBefore));
    }
}
