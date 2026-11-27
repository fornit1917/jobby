using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyDashboardCommandStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class RequestGroupUnlockTests
{
    private static JobDbModel Locker(string group, JobStatus status, string? serverId = null,
        bool canBeRestarted = false) => new()
    {
        Id = Guid.NewGuid(), JobName = "locker", Status = status, QueueName = "default",
        CreatedAt = DateTime.UtcNow, ScheduledStartAt = DateTime.UtcNow,
        SerializableGroupId = group, LockGroupIfFailed = true,
        ServerId = serverId, CanBeRestarted = canBeRestarted,
        StartedCount = status == JobStatus.Failed ? 1 : 0,
    };

    [Fact]
    public async Task StuckGroup_InsertsOnce_AndIsIdempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.Add(Locker("g1", JobStatus.Failed));
        await db.SaveChangesAsync(cancellationToken);

        var storage = DbHelper.CreateDashboardCommandStorage();
        var first = await storage.RequestGroupUnlockAsync("g1", DateTime.UtcNow, cancellationToken);
        var second = await storage.RequestGroupUnlockAsync("g1", DateTime.UtcNow, cancellationToken);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        var rows = await db.UnlockingGroups.CountAsync(u => u.GroupId == "g1", cancellationToken);
        Assert.Equal(1, rows);
    }

    [Fact]
    public async Task NonStuckGroup_InsertsNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Servers.Add(new ServerDbModel { Id = "srv-live", HeartbeatTs = DateTime.UtcNow });
        db.Jobs.Add(Locker("g1", JobStatus.Processing, serverId: "srv-live", canBeRestarted: false));
        await db.SaveChangesAsync(cancellationToken);

        var affected = await DbHelper.CreateDashboardCommandStorage()
            .RequestGroupUnlockAsync("g1", DateTime.UtcNow, cancellationToken);

        Assert.Equal(0, affected);
        var rows = await db.UnlockingGroups.CountAsync(u => u.GroupId == "g1", cancellationToken);
        Assert.Equal(0, rows);
    }

    [Fact]
    public async Task UnknownGroup_InsertsNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        await db.SaveChangesAsync(cancellationToken);

        var affected = await DbHelper.CreateDashboardCommandStorage()
            .RequestGroupUnlockAsync("nope", DateTime.UtcNow, cancellationToken);

        Assert.Equal(0, affected);
    }

    [Fact]
    public async Task RecurrentStuckLocker_InsertsNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var recurrent = Locker("g1", JobStatus.Failed);
        recurrent.Schedule = "0 0 * * *";
        db.Jobs.Add(recurrent);
        await db.SaveChangesAsync(cancellationToken);

        var affected = await DbHelper.CreateDashboardCommandStorage()
            .RequestGroupUnlockAsync("g1", DateTime.UtcNow, cancellationToken);

        Assert.Equal(0, affected);
        Assert.Equal(0, await db.UnlockingGroups.CountAsync(u => u.GroupId == "g1", cancellationToken));
    }
}