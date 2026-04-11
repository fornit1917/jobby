using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlPermanentLocksStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class FreezePermanentLockedJobsFromTopOfQueueTests
{
    [Fact]
    public async Task FreezePermanentLockedJobsFromTopOfQueue_FreezesLockedByFailedJobFromSpecifiedQueue()
    {
        var queueName = Guid.NewGuid().ToString();
        var groupId  = Guid.NewGuid().ToString();
        var dbContext = DbHelper.CreateContext();

        var permanentLocker = new JobDbModel
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Failed,
            SerializableGroupId = groupId,
            LockGroupIfFailed = true,
            StartedCount = 1,
            QueueName = queueName,
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(-10),
            JobName = "test",
            CreatedAt = DateTime.UtcNow,
        };
        var permanentLockedJob = new JobDbModel
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Scheduled,
            SerializableGroupId = groupId,
            QueueName = queueName,
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(-9),
            JobName = "test",
            CreatedAt = DateTime.UtcNow,
        };
        var permanentLockedJobFromOtherQueue = new JobDbModel
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Scheduled,
            SerializableGroupId = groupId,
            QueueName = "other",
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(-8),
            JobName = "test",
            CreatedAt = DateTime.UtcNow,
        };
        
        dbContext.AddRange([permanentLocker, permanentLockedJob, permanentLockedJobFromOtherQueue]);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreatePermanentLockedGroupsStorage();
        var frozenJobs = new List<JobWithGroupModel>();
        await storage.FreezePermanentLockedJobsFromTopOfQueue(queueName, 10, frozenJobs);
        
        Assert.Single(frozenJobs);
        Assert.Equal(permanentLockedJob.Id, frozenJobs[0].Id);
        Assert.Equal(permanentLockedJob.JobName, frozenJobs[0].JobName);
        Assert.Equal(permanentLockedJob.SerializableGroupId, frozenJobs[0].GroupId);
        
        var actualPermanentLockedJob = await dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == permanentLockedJob.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualPermanentLockedJob);
        Assert.Equal(JobStatus.Frozen, actualPermanentLockedJob.Status);
        
        var actualFromOtherQueue = await dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == permanentLockedJobFromOtherQueue.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualFromOtherQueue);
        Assert.Equal(JobStatus.Scheduled, actualFromOtherQueue.Status);
    }
    
    [Fact]
    public async Task FreezePermanentLockedJobsFromTopOfQueue_FreezesLockedByStuck()
    {
        var queueName = Guid.NewGuid().ToString();
        var groupId = Guid.NewGuid().ToString();
        var dbContext = DbHelper.CreateContext();

        var permanentLocker = new JobDbModel
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Processing,
            CanBeRestarted = false,
            ServerId = "lost-server",
            SerializableGroupId = groupId,
            LockGroupIfFailed = false,
            StartedCount = 1,
            QueueName = queueName,
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(-10),
            JobName = "test",
            CreatedAt = DateTime.UtcNow,
        };
        var permanentLockedJob = new JobDbModel
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Scheduled,
            SerializableGroupId = groupId,
            QueueName = queueName,
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(-9),
            JobName = "test",
            CreatedAt = DateTime.UtcNow,
        };
        
        dbContext.AddRange([permanentLocker, permanentLockedJob]);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreatePermanentLockedGroupsStorage();
        var frozenJobs = new List<JobWithGroupModel>();
        await storage.FreezePermanentLockedJobsFromTopOfQueue(queueName, 10, frozenJobs);
        
        Assert.Single(frozenJobs);
        Assert.Equal(permanentLockedJob.Id, frozenJobs[0].Id);
        Assert.Equal(permanentLockedJob.JobName, frozenJobs[0].JobName);
        Assert.Equal(permanentLockedJob.SerializableGroupId, frozenJobs[0].GroupId);
        
        var actualPermanentLockedJob = await dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == permanentLockedJob.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualPermanentLockedJob);
        Assert.Equal(JobStatus.Frozen, actualPermanentLockedJob.Status);
    }
    
    [Fact]
    public async Task FreezePermanentLockedJobsFromTopOfQueue_DoesNotFreezeLockedByNotStuck()
    {
        var queueName = Guid.NewGuid().ToString();
        var groupId = Guid.NewGuid().ToString();
        var serverId = Guid.NewGuid().ToString();
        var dbContext = DbHelper.CreateContext();

        var server = new ServerDbModel
        {
            Id = serverId,
            HeartbeatTs = DateTime.UtcNow,
        };
        dbContext.Servers.Add(server);

        var notPermanentLocker = new JobDbModel
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Processing,
            CanBeRestarted = false,
            ServerId = serverId,
            SerializableGroupId = groupId,
            LockGroupIfFailed = false,
            StartedCount = 1,
            QueueName = queueName,
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(-10),
            JobName = "test",
            CreatedAt = DateTime.UtcNow,
        };
        var notPermanentLockedJob = new JobDbModel
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Scheduled,
            SerializableGroupId = groupId,
            QueueName = queueName,
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(-9),
            JobName = "test",
            CreatedAt = DateTime.UtcNow,
        };
        
        dbContext.AddRange([notPermanentLocker, notPermanentLockedJob]);
        
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreatePermanentLockedGroupsStorage();
        var frozenJobs = new List<JobWithGroupModel>();
        await storage.FreezePermanentLockedJobsFromTopOfQueue(queueName, 10, frozenJobs);
        
        Assert.Empty(frozenJobs);
        
        var actualNotPermanentLockedJob = await dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == notPermanentLockedJob.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualNotPermanentLockedJob);
        Assert.Equal(JobStatus.Scheduled, actualNotPermanentLockedJob.Status);
    }
    
    [Fact]
    public async Task FreezePermanentLockedJobsFromTopOfQueue_DoesNotFreezeLockedByWaitingForRetry()
    {
        var queueName = Guid.NewGuid().ToString();
        var groupId = Guid.NewGuid().ToString();
        var dbContext = DbHelper.CreateContext();

        var notPermanentLocker = new JobDbModel
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Scheduled,
            StartedCount = 1,
            SerializableGroupId = groupId,
            LockGroupIfFailed = true,
            QueueName = queueName,
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(10),
            JobName = "test",
            CreatedAt = DateTime.UtcNow,
        };
        var notPermanentLockedJob = new JobDbModel
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Scheduled,
            SerializableGroupId = groupId,
            QueueName = queueName,
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(-9),
            JobName = "test",
            CreatedAt = DateTime.UtcNow,
        };
        
        dbContext.AddRange([notPermanentLocker, notPermanentLockedJob]);
        
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreatePermanentLockedGroupsStorage();
        var frozenJobs = new List<JobWithGroupModel>();
        await storage.FreezePermanentLockedJobsFromTopOfQueue(queueName, 10, frozenJobs);
        
        Assert.Empty(frozenJobs);
        
        var actualNotPermanentLockedJob = await dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == notPermanentLockedJob.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualNotPermanentLockedJob);
        Assert.Equal(JobStatus.Scheduled, actualNotPermanentLockedJob.Status);
    }
    
    [Fact]
    public async Task FreezePermanentLockedJobsFromTopOfQueue_DoesNotFreezeJobWithoutGroup()
    {
        var queueName = Guid.NewGuid().ToString();
        var groupId  = Guid.NewGuid().ToString();
        var dbContext = DbHelper.CreateContext();

        var permanentLocker = new JobDbModel
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Failed,
            SerializableGroupId = groupId,
            LockGroupIfFailed = true,
            StartedCount = 1,
            QueueName = queueName,
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(-10),
            JobName = "test",
            CreatedAt = DateTime.UtcNow,
        };
        var jobWithoutGroup = new JobDbModel
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Scheduled,
            SerializableGroupId = null,
            QueueName = queueName,
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(-9),
            JobName = "test",
            CreatedAt = DateTime.UtcNow,
        };
        
        dbContext.AddRange([permanentLocker, jobWithoutGroup]);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreatePermanentLockedGroupsStorage();
        var frozenJobs = new List<JobWithGroupModel>();
        await storage.FreezePermanentLockedJobsFromTopOfQueue(queueName, 10, frozenJobs);
        
        Assert.Empty(frozenJobs);
        
        var actualJobWithoutGroup = await dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobWithoutGroup.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualJobWithoutGroup);
        Assert.Equal(JobStatus.Scheduled, actualJobWithoutGroup.Status);
    }    
}