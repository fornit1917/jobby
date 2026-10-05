using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.IntegrationTests.Postgres.Helpers;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyDashboardReadStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class GetLockedGroupsTests
{
    private static readonly LockedGroupsQuery All = new() { Page = 1, PageSize = 100 };

    private static JobDbModel Job(JobStatus status, string group, bool lockIfFailed = false,
        string? serverId = null, bool canBeRestarted = false, string queue = "default") => new()
    {
        Id = Guid.NewGuid(),
        JobName = "job",
        Status = status,
        QueueName = queue,
        CreatedAt = DateTime.UtcNow.AddMinutes(-30),
        ScheduledStartAt = DateTime.UtcNow,
        SerializableGroupId = group,
        LockGroupIfFailed = lockIfFailed,
        ServerId = serverId,
        CanBeRestarted = canBeRestarted,
        StartedCount = status == JobStatus.Failed ? 1 : 0,
    };

    [Fact]
    public async Task FailedLockerWithFrozenSiblings_Appears()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.Add(Job(JobStatus.Failed, "g1", lockIfFailed: true));
        db.Jobs.Add(Job(JobStatus.Frozen, "g1"));
        db.Jobs.Add(Job(JobStatus.Frozen, "g1"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DbHelper.CreateDashboardReadStorage()
            .GetLockedGroupsAsync(All, TestContext.Current.CancellationToken);

        var g = Assert.Single(result.Items);
        Assert.Equal("g1", g.GroupId);
        Assert.Equal(JobStatus.Failed, g.LockerStatus);
        Assert.Equal(2, g.FrozenCount);
        Assert.Equal(0, g.NonLockerFailedCount);
        Assert.False(g.UnlockRequested);
    }

    [Fact]
    public async Task FailedLockerWithZeroFrozenSiblings_StillAppears()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.Add(Job(JobStatus.Failed, "g1", lockIfFailed: true));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DbHelper.CreateDashboardReadStorage()
            .GetLockedGroupsAsync(All, TestContext.Current.CancellationToken);

        var g = Assert.Single(result.Items);
        Assert.Equal(0, g.FrozenCount);
    }

    [Fact]
    public async Task ProcessingLockerOnDeadServer_Appears()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.Add(Job(JobStatus.Processing, "g1", serverId: "ghost", canBeRestarted: false));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DbHelper.CreateDashboardReadStorage()
            .GetLockedGroupsAsync(All, TestContext.Current.CancellationToken);

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task ProcessingLockerOnLiveServer_DoesNotAppear()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Servers.Add(new ServerDbModel { Id = "srv-live", HeartbeatTs = DateTime.UtcNow });
        db.Jobs.Add(Job(JobStatus.Processing, "g1", serverId: "srv-live", canBeRestarted: false));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DbHelper.CreateDashboardReadStorage()
            .GetLockedGroupsAsync(All, TestContext.Current.CancellationToken);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ScheduledRetryLocker_DoesNotAppear()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var retry = Job(JobStatus.Scheduled, "g1", lockIfFailed: true);
        retry.StartedCount = 1;
        db.Jobs.Add(retry);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DbHelper.CreateDashboardReadStorage()
            .GetLockedGroupsAsync(All, TestContext.Current.CancellationToken);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task CountsAndQueues_AreAggregated_ExcludingLockerFromFailedCount()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.Add(Job(JobStatus.Failed, "g1", lockIfFailed: true, queue: "a"));
        db.Jobs.Add(Job(JobStatus.Frozen, "g1", queue: "a"));
        db.Jobs.Add(Job(JobStatus.Scheduled, "g1", queue: "b"));
        db.Jobs.Add(Job(JobStatus.WaitingPrev, "g1", queue: "a"));
        var extraFailed = Job(JobStatus.Failed, "g1", queue: "a");
        db.Jobs.Add(extraFailed);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DbHelper.CreateDashboardReadStorage()
            .GetLockedGroupsAsync(All, TestContext.Current.CancellationToken);

        var g = Assert.Single(result.Items);
        Assert.Equal(1, g.FrozenCount);
        Assert.Equal(1, g.ScheduledCount);
        Assert.Equal(1, g.WaitingCount);
        Assert.Equal(1, g.NonLockerFailedCount);
        Assert.Equal(["a", "b"], g.Queues);
    }

    [Fact]
    public async Task UnlockRequested_ReflectsUnlockingGroupsRow()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.Add(Job(JobStatus.Failed, "g1", lockIfFailed: true));
        db.UnlockingGroups.Add(new UnlockingGroupDbModel { GroupId = "g1", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DbHelper.CreateDashboardReadStorage()
            .GetLockedGroupsAsync(All, TestContext.Current.CancellationToken);

        var g = Assert.Single(result.Items);
        Assert.True(g.UnlockRequested);
        Assert.NotNull(g.UnlockRequestedAt);
    }

    [Fact]
    public async Task Ordering_UnrequestedBeforeRequested()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.Add(Job(JobStatus.Failed, "requested", lockIfFailed: true));
        db.Jobs.Add(Job(JobStatus.Failed, "unrequested", lockIfFailed: true));
        db.UnlockingGroups.Add(new UnlockingGroupDbModel { GroupId = "requested", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DbHelper.CreateDashboardReadStorage()
            .GetLockedGroupsAsync(All, TestContext.Current.CancellationToken);

        Assert.Equal("unrequested", result.Items[0].GroupId);
        Assert.Equal("requested", result.Items[1].GroupId);
    }

    [Fact]
    public async Task Ordering_OldestStuckFirst_AmongUnrequested()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var older = Job(JobStatus.Failed, "older", lockIfFailed: true);
        older.LastFinishedAt = DateTime.UtcNow.AddHours(-3);
        var newer = Job(JobStatus.Failed, "newer", lockIfFailed: true);
        newer.LastFinishedAt = DateTime.UtcNow.AddMinutes(-10);
        db.Jobs.AddRange(newer, older);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DbHelper.CreateDashboardReadStorage()
            .GetLockedGroupsAsync(All, TestContext.Current.CancellationToken);

        Assert.Equal("older", result.Items[0].GroupId);
        Assert.Equal("newer", result.Items[1].GroupId);
    }

    [Fact]
    public async Task RecurrentStuckLocker_DoesNotAppear()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var recurrent = Job(JobStatus.Failed, "g1", lockIfFailed: true);
        recurrent.Schedule = "0 0 * * *";
        db.Jobs.Add(recurrent);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DbHelper.CreateDashboardReadStorage()
            .GetLockedGroupsAsync(All, TestContext.Current.CancellationToken);

        Assert.Empty(result.Items);
    }
}