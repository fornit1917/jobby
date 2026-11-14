using Bunit;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.UI.Shared;
using Microsoft.AspNetCore.Components;
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

    private IRenderedComponent<Bunit.Rendering.ContainerFragment> RenderMenu(
        JobStatus status, bool isGroupLocker, bool recurrent = false, Guid? id = null, Action? onChanged = null) =>
        Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<JobActionsMenu>(1);
            builder.AddAttribute(2, nameof(JobActionsMenu.Id), id ?? Guid.NewGuid());
            builder.AddAttribute(3, nameof(JobActionsMenu.Status), status);
            builder.AddAttribute(4, nameof(JobActionsMenu.IsGroupLocker), isGroupLocker);
            builder.AddAttribute(5, nameof(JobActionsMenu.Recurrent), recurrent);
            if (onChanged is not null)
            {
                builder.AddAttribute(6, nameof(JobActionsMenu.OnChanged), EventCallback.Factory.Create(this, onChanged));
            }
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScheduledGroupLocker_KeepsTriggerHidesCancel(bool recurrent)
    {
        await SetupManagementAsync();
        var cut = RenderMenu(JobStatus.Scheduled, isGroupLocker: true, recurrent: recurrent);

        await cut.Find("[data-testid=job-actions] button").ClickAsync();
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("[data-testid=action-trigger]")));
        Assert.Empty(cut.FindAll("[data-testid=action-cancel]"));
        Assert.Empty(cut.FindAll("[data-testid=action-delete-recurrent]"));
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

    [Theory]
    [InlineData(JobStatus.Processing)]
    [InlineData(JobStatus.Frozen)]
    [InlineData(JobStatus.Failed)]
    [InlineData(JobStatus.Completed)]
    [InlineData(JobStatus.WaitingPrev)]
    public async Task NonScheduledRecurrentNonLocker_ShowsOnlyDelete(JobStatus status)
    {
        await SetupManagementAsync();
        var cut = RenderMenu(status, isGroupLocker: false, recurrent: true);

        await cut.Find("[data-testid=job-actions] button").ClickAsync();
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("[data-testid=action-delete-recurrent]")));
        Assert.Empty(cut.FindAll("[data-testid=action-trigger]"));
        Assert.Empty(cut.FindAll("[data-testid=action-retry]"));
        Assert.Empty(cut.FindAll("[data-testid=action-cancel]"));
        Assert.Empty(cut.FindAll("[data-testid=action-delete]"));
    }

    [Fact]
    public async Task ScheduledRecurrentNonLocker_ShowsTriggerAndDeleteThenTriggerPostsAndNotifiesChange()
    {
        await SetupManagementAsync();
        var id = Guid.NewGuid();
        var changeCount = 0;
        var cut = RenderMenu(JobStatus.Scheduled, isGroupLocker: false, recurrent: true, id: id,
            onChanged: () => changeCount++);

        await cut.Find("[data-testid=job-actions] button").ClickAsync();
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll("[data-testid=action-trigger]")));
        Assert.NotEmpty(cut.FindAll("[data-testid=action-delete-recurrent]"));
        Assert.Empty(cut.FindAll("[data-testid=action-retry]"));
        Assert.Empty(cut.FindAll("[data-testid=action-cancel]"));
        Assert.Empty(cut.FindAll("[data-testid=action-delete]"));

        await cut.Find("[data-testid=action-trigger]").ClickAsync();

        await cut.WaitForAssertionAsync(() => Assert.Equal(1, changeCount));
        var request = Assert.Single(Handler.RequestsTo($"/api/jobs/{id}/trigger"));
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("tok", Assert.Single(request.Headers.GetValues("X-Jobby-Dashboard")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadOnlyView_HidesMenu_EvenWhenManagementEnabled(bool recurrent)
    {
        await SetupManagementAsync();
        Services.GetRequiredService<Jobby.Dashboard.UI.DashboardModeState>().Mode =
            Jobby.Dashboard.UI.DashboardMode.ReadOnly;

        var cut = Render<JobActionsMenu>(p => p
            .Add(x => x.Id, Guid.NewGuid())
            .Add(x => x.Status, JobStatus.Scheduled)
            .Add(x => x.Recurrent, recurrent)
            .Add(x => x.IsGroupLocker, false));

        Assert.Empty(cut.FindAll("[data-testid=job-actions]"));
    }

    [Fact]
    public async Task ScheduledRecurrent_WhenManagementDisabled_ShowsNoMenu()
    {
        await SetupApiAndLoadConfigAsync(req => req.RequestUri!.AbsolutePath == "/api/config"
            ? new JobbyDashboardClientConfigDto { ManagementEnabled = false }
            : null);

        var cut = Render<JobActionsMenu>(p => p
            .Add(x => x.Id, Guid.NewGuid())
            .Add(x => x.Status, JobStatus.Scheduled)
            .Add(x => x.Recurrent, true));

        Assert.Empty(cut.FindAll("[data-testid=job-actions]"));
    }
}
