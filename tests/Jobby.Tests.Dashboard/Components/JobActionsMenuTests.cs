using Bunit;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client.Shared;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Jobby.Tests.Dashboard.Components;

public class JobActionsMenuTests : DashboardClientTestContext
{
    private static JobbyDashboardClientConfigDto ManagementConfig() => new()
    {
        ManagementEnabled = true,
        ManagementRequestHeaderName = "X-Jobby-Dashboard",
        ManagementRequestToken = "tok",
    };

    private Task SetupManagementAsync() =>
        SetupApiAndLoadConfigAsync(req => req.RequestUri!.AbsolutePath == "/api/config" ? ManagementConfig() : null);

    private IRenderedComponent<Bunit.Rendering.ContainerFragment> RenderMenu(JobStatus status, bool isGroupLocker) =>
        Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<JobActionsMenu>(1);
            builder.AddAttribute(2, nameof(JobActionsMenu.Id), Guid.NewGuid());
            builder.AddAttribute(3, nameof(JobActionsMenu.Status), status);
            builder.AddAttribute(4, nameof(JobActionsMenu.IsGroupLocker), isGroupLocker);
            builder.CloseComponent();
        });

    [Fact]
    public async Task FailedGroupLocker_ShowsNoMenu()
    {
        await SetupManagementAsync();

        var cut = Render<JobActionsMenu>(p => p
            .Add(x => x.Id, Guid.NewGuid())
            .Add(x => x.Status, JobStatus.Failed)
            .Add(x => x.IsGroupLocker, true));

        Assert.Empty(cut.FindAll("[data-testid=job-actions]"));
    }

    [Fact]
    public async Task ScheduledGroupLocker_KeepsTriggerHidesCancel()
    {
        await SetupManagementAsync();
        var cut = RenderMenu(JobStatus.Scheduled, isGroupLocker: true);

        await cut.Find("[data-testid=job-actions] button").ClickAsync();
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("[data-testid=action-trigger]")));
        Assert.Empty(cut.FindAll("[data-testid=action-cancel]"));
    }

    [Fact]
    public async Task NonLockerFailed_ShowsRetryAndDelete()
    {
        await SetupManagementAsync();
        var cut = RenderMenu(JobStatus.Failed, isGroupLocker: false);

        await cut.Find("[data-testid=job-actions] button").ClickAsync();
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("[data-testid=action-retry]")));
        Assert.NotEmpty(cut.FindAll("[data-testid=action-delete]"));
    }

    [Fact]
    public async Task RecurrentGroupLocker_ShowsNoMenu()
    {
        await SetupManagementAsync();

        var cut = Render<JobActionsMenu>(p => p
            .Add(x => x.Id, Guid.NewGuid())
            .Add(x => x.Status, JobStatus.Processing)
            .Add(x => x.Recurrent, true)
            .Add(x => x.IsGroupLocker, true));

        Assert.Empty(cut.FindAll("[data-testid=job-actions]"));
    }

    [Fact]
    public async Task RecurrentNonLocker_ShowsDelete()
    {
        await SetupManagementAsync();
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<JobActionsMenu>(1);
            builder.AddAttribute(2, nameof(JobActionsMenu.Id), Guid.NewGuid());
            builder.AddAttribute(3, nameof(JobActionsMenu.Recurrent), true);
            builder.AddAttribute(4, nameof(JobActionsMenu.IsGroupLocker), false);
            builder.CloseComponent();
        });

        await cut.Find("[data-testid=job-actions] button").ClickAsync();
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("[data-testid=action-delete-recurrent]")));
    }

    [Fact]
    public async Task ReadOnlyView_HidesMenu_EvenWhenManagementEnabled()
    {
        await SetupManagementAsync();
        Services.GetRequiredService<Jobby.Dashboard.Client.DashboardModeState>().Mode =
            Jobby.Dashboard.Client.DashboardMode.ReadOnly;

        var cut = Render<JobActionsMenu>(p => p
            .Add(x => x.Id, Guid.NewGuid())
            .Add(x => x.Status, JobStatus.Failed)
            .Add(x => x.IsGroupLocker, false));

        Assert.Empty(cut.FindAll("[data-testid=job-actions]"));
    }
}