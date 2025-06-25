using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection("Jobby.Postgres.IntegrationTests")]
public class HeartbeatAndRestartMethodsTests
{
    [Fact]
    public async Task SendHeartbeatAsync_FirstTime_Inserts()
    {
        var serverId = Guid.NewGuid().ToString();
        var storage = DbHelper.CreateJobbyStorage();

        await storage.SendHeartbeatAsync(serverId);

        await using var dbContext = DbHelper.CreateContext();
        var actualServer = await dbContext.Servers.AsNoTracking().Where(x => x.Id == serverId).FirstOrDefaultAsync();
        Assert.NotNull(actualServer);
        Assert.Equal(DateTime.UtcNow, actualServer.HeartbeatTs, TimeSpan.FromSeconds(3));
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
        await dbContext.AddAsync(server);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.SendHeartbeatAsync(server.Id);

        var actualServer = await dbContext.Servers.AsNoTracking().Where(x => x.Id == server.Id).FirstOrDefaultAsync();
        Assert.NotNull(actualServer);
        Assert.Equal(DateTime.UtcNow, actualServer.HeartbeatTs, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task DeleteLostServersAndRestartTheirJobsAsync_DeletesServersAndRestartRestarableJobs()
    {
        await using var dbContext = DbHelper.CreateContextAndClearDb();

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
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        var deletedServerIds = new List<string>();
        var stuckJobs = new List<StuckJobModel>();
        await storage.DeleteLostServersAndRestartTheirJobsAsync(DateTime.UtcNow.AddMinutes(-1), deletedServerIds, stuckJobs);

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

        var lostServerExists = await dbContext.Servers.AsNoTracking().AnyAsync(x => x.Id == lostServer.Id);
        Assert.False(lostServerExists);

        var aliveServerExists = await dbContext.Servers.AsNoTracking().AnyAsync(x => x.Id == aliveServer.Id);
        Assert.True(aliveServerExists);

        var restartableActualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == restartableJob.Id);
        Assert.Equal(JobStatus.Scheduled, restartableActualJob.Status);
        var notRestartableActualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == notRestartableJob.Id);
        Assert.Equal(JobStatus.Processing, notRestartableActualJob.Status);
    }
}
