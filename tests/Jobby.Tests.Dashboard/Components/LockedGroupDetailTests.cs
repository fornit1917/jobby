using Bunit;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Jobby.Tests.Dashboard.Components;

public class LockedGroupDetailTests : DashboardClientTestContext
{
    private static LockedGroupDetailsDto Details() => new(
        GroupId: "order-4521", Queues: ["default"], LockerId: Guid.NewGuid(), LockerName: "SyncOrder",
        LockerStatus: JobStatus.Failed, LockerServerId: null, LockerLastStartedAt: null, LockerLastFinishedAt: null,
        LockerCreatedAt: DateTime.UtcNow, LockerError: null, StuckSince: DateTime.UtcNow.AddHours(-2),
        FrozenCount: 1, ScheduledCount: 1, WaitingCount: 0, NonLockerFailedCount: 0, OldestFrozenAt: null,
        UnlockRequested: false, UnlockRequestedAt: null,
        Jobs:
        [
            new(Id: Guid.NewGuid(), JobName: "SyncOrder", Status: JobStatus.Failed, ScheduledStartAt: DateTime.UtcNow,
                StartedCount: 1, ServerId: null, Role: LockedGroupJobRole.Locker),
            new(Id: Guid.NewGuid(), JobName: "SyncOrder", Status: JobStatus.Frozen,
                ScheduledStartAt: DateTime.UtcNow.AddMinutes(1), StartedCount: 0, ServerId: null,
                Role: LockedGroupJobRole.Frozen),
            new(Id: Guid.NewGuid(), JobName: "SyncOrder", Status: JobStatus.Scheduled,
                ScheduledStartAt: DateTime.UtcNow.AddMinutes(2), StartedCount: 0, ServerId: null,
                Role: LockedGroupJobRole.Scheduled),
        ]);

    private void NavigateToGroup(string id) =>
        Services.GetRequiredService<NavigationManager>().NavigateTo($"locked-groups/detail?groupId={id}");

    [Fact]
    public void Renders_LockerHeaderAndRoleLabeledJobs()
    {
        SetupApi(req => req.RequestUri!.AbsolutePath == "/api/locked-groups/detail" ? Details() : null);
        NavigateToGroup("order-4521");

        var cut = Render<LockedGroupDetail>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=locked-group-jobs]")));
        Assert.Contains("Locker", cut.Markup);
        Assert.Contains("Frozen", cut.Markup);
        Assert.Contains("Scheduled", cut.Markup);
    }

    [Fact]
    public void Shows_NotLockedWhenDetailIsNull()
    {
        SetupApi(_ => null);
        NavigateToGroup("order-4521");

        var cut = Render<LockedGroupDetail>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=locked-group-notfound]")));
    }
}