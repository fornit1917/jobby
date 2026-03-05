using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlPermanentLocksStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class UnfreezeBatchAndUnlockIfAllUnfrozenTests
{
    [Fact]
    public async Task UnfreezeBatchAndUnlockIfAllUnfrozen_NoUnlockingRequests_DoesNothingAndReturnsEmpty()
    {
        var queueName = Guid.NewGuid().ToString();
        var groupId  = Guid.NewGuid().ToString();
        var dbContext = await DbHelper.CreateContextAndClearDbAsync();

        var locker = new JobDbModel
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
        var frozen = new JobDbModel
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Frozen,
            SerializableGroupId = groupId,
            LockGroupIfFailed = true,
            StartedCount = 0,
            QueueName = queueName,
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(-10),
            JobName = "test",
            CreatedAt = DateTime.UtcNow,
        };
        await dbContext.AddRangeAsync(locker, frozen);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        
        var storage = DbHelper.CreatePermanentLockedGroupsStorage();
        var unlockingStatus = await storage.UnfreezeBatchAndUnlockIfAllUnfrozen();
        
        Assert.Null(unlockingStatus);
        var actualLocker = await dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == locker.Id, 
                cancellationToken: TestContext.Current.CancellationToken);
        var actualFrozen = await dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == frozen.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualLocker);
        Assert.Equal(JobStatus.Failed, actualLocker.Status);
        Assert.NotNull(actualFrozen);
        Assert.Equal(JobStatus.Frozen, actualFrozen.Status);
    }
    
    [Fact]
    public async Task UnfreezeBatchAndUnlockIfAllUnfrozen_HasFrozen_UnfreezesJobsAndDoesNotUnlocks()
    {
        var queueName = Guid.NewGuid().ToString();
        var groupId  = Guid.NewGuid().ToString();
        var dbContext = await DbHelper.CreateContextAndClearDbAsync();

        var locker = new JobDbModel
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
        var frozen = new JobDbModel
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Frozen,
            SerializableGroupId = groupId,
            LockGroupIfFailed = true,
            StartedCount = 0,
            QueueName = queueName,
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(-10),
            JobName = "test",
            CreatedAt = DateTime.UtcNow,
        };
        await dbContext.AddRangeAsync(locker, frozen);

        var unlockingRequest = new UnlockingGroupDbModel
        {
            GroupId = groupId,
            CreatedAt = DateTime.UtcNow.AddSeconds(-10),
        };
        await dbContext.AddAsync(unlockingRequest, TestContext.Current.CancellationToken);
        
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        
        var storage = DbHelper.CreatePermanentLockedGroupsStorage();
        var unlockingStatus = await storage.UnfreezeBatchAndUnlockIfAllUnfrozen();
        
        Assert.NotNull(unlockingStatus);
        Assert.Equal(groupId, unlockingStatus.GroupId);
        Assert.False(unlockingStatus.IsUnlocked);
        
        var actualLocker = await dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == locker.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        var actualFrozen = await dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == frozen.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualLocker);
        Assert.Equal(JobStatus.Failed, actualLocker.Status);
        Assert.NotNull(actualFrozen);
        Assert.Equal(JobStatus.Scheduled, actualFrozen.Status);
        
        var actualUnlockingRequest = await dbContext.UnlockingGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GroupId == groupId,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualUnlockingRequest);
    }    

    [Fact]
    public async Task UnfreezeBatchAndUnlockIfAllUnfrozen_DoesNotHaveFrozen_UnlocksGroup()
    {
        var queueName = Guid.NewGuid().ToString();
        var groupId  = Guid.NewGuid().ToString();
        var dbContext = await DbHelper.CreateContextAndClearDbAsync();

        var locker = new JobDbModel
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Failed,
            SerializableGroupId = groupId,
            LockGroupIfFailed = true,
            StartedCount = 0,
            QueueName = queueName,
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(-10),
            JobName = "test",
            CreatedAt = DateTime.UtcNow,
        };
        await dbContext.AddAsync(locker, TestContext.Current.CancellationToken);

        var unlockingRequest = new UnlockingGroupDbModel
        {
            GroupId = groupId,
            CreatedAt = DateTime.UtcNow.AddSeconds(-10),
        };
        await dbContext.AddAsync(unlockingRequest, TestContext.Current.CancellationToken);
        
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        
        var storage = DbHelper.CreatePermanentLockedGroupsStorage();
        var unlockingStatus = await storage.UnfreezeBatchAndUnlockIfAllUnfrozen();
        
        Assert.NotNull(unlockingStatus);
        Assert.Equal(groupId, unlockingStatus.GroupId);
        Assert.True(unlockingStatus.IsUnlocked);
        
        var actualLocker = await dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == locker.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Null(actualLocker);
        
        var actualUnlockingRequest = await dbContext.UnlockingGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GroupId == groupId,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Null(actualUnlockingRequest);
    }
}