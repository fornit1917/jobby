using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class HeartbeatAndRestartMethodsTests
{
    [Fact]
    public async Task SendHeartbeatAsync_FirstTime_Inserts()
    {
        var serverId = Guid.NewGuid().ToString();
        var storage = DbHelper.CreateJobbyStorage();

        await storage.SendHeartbeatAsync(serverId);

        var dbNow = await GetDbNowAsync();
        await using var dbContext = DbHelper.CreateContext();
        var actualServer = await dbContext.Servers.AsNoTracking()
            .Where(x => x.Id == serverId)
            .FirstOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualServer);
        // heartbeat_ts must be written with the database clock, not the application clock
        Assert.Equal(dbNow, actualServer.HeartbeatTs, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task SendHeartbeatAsync_NotFirstTime_Updates()
    {
        var server = new ServerDbModel
        {
            Id = Guid.NewGuid().ToString(),
            HeartbeatTs = DateTime.UtcNow.AddDays(10)
        };
        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddAsync(server, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateJobbyStorage();
        await storage.SendHeartbeatAsync(server.Id);

        var dbNow = await GetDbNowAsync();
        var actualServer = await dbContext.Servers.AsNoTracking()
            .Where(x => x.Id == server.Id)
            .FirstOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualServer);
        // heartbeat_ts must be written with the database clock, not the application clock
        Assert.Equal(dbNow, actualServer.HeartbeatTs, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task DeleteLostServersAndRestartTheirJobsAsync_ByInterval_DeletesServersAndRestartRestartableJobs()
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();

        var aliveServer = new ServerDbModel
        {
            Id = Guid.NewGuid().ToString(),
            HeartbeatTs = DateTime.UtcNow.AddDays(1)
        };
        var lostServer = new ServerDbModel
        {
            Id = Guid.NewGuid().ToString(),
            HeartbeatTs = DateTime.UtcNow
        };
        var restartableJob = new JobDbModel
        {
            Id = Guid.NewGuid(),
            ServerId = lostServer.Id,
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            Status = JobStatus.Processing,
            ScheduledStartAt = DateTime.UtcNow,
            CanBeRestarted = true,
        };
        var notRestartableJob = new JobDbModel
        {
            Id = Guid.NewGuid(),
            ServerId = lostServer.Id,
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            Status = JobStatus.Processing,
            ScheduledStartAt = DateTime.UtcNow,
            CanBeRestarted = false,
        };
        await dbContext.Servers.AddRangeAsync([lostServer, aliveServer]);
        await dbContext.Jobs.AddRangeAsync([restartableJob, notRestartableJob]);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Backdate the heartbeat relative to the database clock, so the test
        // does not depend on the app/DB clock sync
        await dbContext.Database.ExecuteSqlAsync(
            $"UPDATE jobby_servers SET heartbeat_ts = now() - interval '1 hour' WHERE id = {lostServer.Id}",
            TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateJobbyStorage();
        var deletedServerIds = new List<string>();
        var stuckJobs = new List<StuckJobModel>();
        await storage.DeleteLostServersAndRestartTheirJobsAsync(TimeSpan.FromMinutes(1), deletedServerIds, stuckJobs);

        Assert.Single(deletedServerIds);
        Assert.Equal(lostServer.Id, deletedServerIds[0]);
        Assert.Equal(2, stuckJobs.Count);
        Assert.Contains(stuckJobs, x => x.Id == restartableJob.Id
                                        && x.CanBeRestarted == true
                                        && x.JobName == restartableJob.JobName
                                        && x.ServerId == lostServer.Id);
        Assert.Contains(stuckJobs, x => x.Id == notRestartableJob.Id
                                        && x.CanBeRestarted == false
                                        && x.JobName == notRestartableJob.JobName
                                        && x.ServerId == notRestartableJob.ServerId);

        var lostServerExists = await dbContext.Servers.AsNoTracking()
            .AnyAsync(x => x.Id == lostServer.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(lostServerExists);

        var aliveServerExists = await dbContext.Servers.AsNoTracking()
            .AnyAsync(x => x.Id == aliveServer.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(aliveServerExists);

        var restartableActualJob = await dbContext.Jobs.AsNoTracking()
            .FirstAsync(x => x.Id == restartableJob.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Scheduled, restartableActualJob.Status);
        var notRestartableActualJob = await dbContext.Jobs.AsNoTracking()
            .FirstAsync(x => x.Id == notRestartableJob.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Processing, notRestartableActualJob.Status);
    }

    // Pins the legacy overload: it compares against an absolute timestamp computed on the
    // application clock (separate SQL path with a timestamptz parameter). A future removal
    // of the obsolete overload deletes this test along with the implementation.
#pragma warning disable CS0618
    [Fact]
    public async Task DeleteLostServersAndRestartTheirJobsAsync_ByMinLastHeartbeat_DeletesServersAndRestartRestartableJobs()
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();

        var aliveServer = new ServerDbModel
        {
            Id = Guid.NewGuid().ToString(),
            HeartbeatTs = DateTime.UtcNow.AddDays(1)
        };
        var lostServer = new ServerDbModel
        {
            Id = Guid.NewGuid().ToString(),
            HeartbeatTs = DateTime.UtcNow.AddDays(-1)
        };
        await dbContext.Servers.AddRangeAsync([lostServer, aliveServer]);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateJobbyStorage();
        var deletedServerIds = new List<string>();
        var stuckJobs = new List<StuckJobModel>();
        await storage.DeleteLostServersAndRestartTheirJobsAsync(DateTime.UtcNow.AddMinutes(-1), deletedServerIds, stuckJobs);

        Assert.Single(deletedServerIds);
        Assert.Equal(lostServer.Id, deletedServerIds[0]);
        Assert.Empty(stuckJobs);

        var lostServerExists = await dbContext.Servers.AsNoTracking()
            .AnyAsync(x => x.Id == lostServer.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(lostServerExists);
        var aliveServerExists = await dbContext.Servers.AsNoTracking()
            .AnyAsync(x => x.Id == aliveServer.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(aliveServerExists);
    }
#pragma warning restore CS0618

    private static async Task<DateTime> GetDbNowAsync()
    {
        await using var dbContext = DbHelper.CreateContext();
        return await dbContext.Database
            .SqlQueryRaw<DateTime>("SELECT now() AS \"Value\"")
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
