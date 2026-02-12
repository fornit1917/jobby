using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class TakeBatchToProcessingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TakeBatchToProcessingAsync_ReturnsReadyToRunAndUpdatesStatusAndCount(bool disableSerializableGroups)
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();

        var firstExpected = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = "firstJob",
            Cron = "*/20 * * * *",
            JobParam = "param1",
            StartedCount = 1,
            NextJobId = Guid.NewGuid(),
            Status = JobStatus.Scheduled,
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(-10),
            QueueName = QueueSettings.DefaultQueueName,
        };
        var secondExpected = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = "secondJob",
            Cron = null,
            JobParam = "param2",
            StartedCount = 0,
            NextJobId = null,
            Status = JobStatus.Scheduled,
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(-5),
            QueueName = QueueSettings.DefaultQueueName,
        };
        var jobs = new List<JobDbModel>
        {
            firstExpected,
            secondExpected,
            
            // should not be returned because the other queue name
            new JobDbModel()
            {
                Id = Guid.NewGuid(),
                JobName = "JobName",
                JobParam = "param",
                Status = JobStatus.Scheduled,
                ScheduledStartAt = DateTime.UtcNow.AddMinutes(-100),
                QueueName = "other"
            },

            // should not be returned because limit is 2
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = "JobName",
                JobParam = "param",
                Status = JobStatus.Scheduled,
                ScheduledStartAt = DateTime.UtcNow.AddMinutes(-1),
            },

            // should not be returned because of status
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = "JobName",
                JobParam = "param",
                Status = JobStatus.WaitingPrev,
                ScheduledStartAt = DateTime.UtcNow.AddMinutes(-100),
            },

            // should not be returned because of time
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = "JobName",
                JobParam = "param",
                Status = JobStatus.Scheduled,
                ScheduledStartAt = DateTime.UtcNow.AddMinutes(100),
            }
        };

        dbContext.Jobs.AddRange(jobs);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        var result = new List<JobExecutionModel>();
        var request = new GetJobsRequest
        {
            QueueName = QueueSettings.DefaultQueueName,
            BatchSize = 2,
            ServerId = "serverId",
            DisableSerializableGroups = disableSerializableGroups
        };
        await storage.TakeBatchToProcessingAsync(request, result);

        Assert.Equal(2, result.Count);
        AssertTakenToRunJob(firstExpected, result[0], request.ServerId);
        AssertTakenToRunJob(secondExpected, result[1], request.ServerId);

        var firstActualFromDb = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == firstExpected.Id);
        AssertUpdatedFields(firstExpected, firstActualFromDb);
        var secondActualFromDb = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == secondExpected.Id);
        AssertUpdatedFields(secondExpected, secondActualFromDb);
    }

    [Fact]
    public async Task TakeBatchToProcessingAsync_ReturnsBatchWithOneJobFromEachNotLockedGroup()
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();

        var jobWithoutGroup = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = "withoutGroup",
            Cron = null,
            JobParam = "param1",
            StartedCount = 0,
            NextJobId = null,
            Status = JobStatus.Scheduled,
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(-5),
            QueueName = QueueSettings.DefaultQueueName,
            SerializableGroupId = null
        };
        var firstJobFromFirstGroup = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = "1_1",
            Cron = null,
            JobParam = "param2",
            StartedCount = 0,
            NextJobId = null,
            Status = JobStatus.Scheduled,
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(-4),
            QueueName = QueueSettings.DefaultQueueName,
            SerializableGroupId = "g_1"
        };
        var firstJobFromSecondGroup = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = "2_1",
            Cron = null,
            JobParam = "param3",
            StartedCount = 0,
            NextJobId = null,
            Status = JobStatus.Scheduled,
            ScheduledStartAt = DateTime.UtcNow.AddMinutes(-2),
            QueueName = QueueSettings.DefaultQueueName,
            SerializableGroupId = "g_2"
        };

        var jobs = new List<JobDbModel>
        {
            // failed, but should not lock group because lock_group_if_failed=false
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = "failed1",
                JobParam = "param",
                StartedCount = 1,
                Status = JobStatus.Failed,
                ScheduledStartAt = DateTime.UtcNow.AddMinutes(-30),
                QueueName = QueueSettings.DefaultQueueName,
                SerializableGroupId = firstJobFromFirstGroup.SerializableGroupId,
                LockGroupIfFailed = false
            },
            // scheduled to retry, but should not lock group because lock_group_if_failed=false
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = "failed2",
                JobParam = "param",
                StartedCount = 1,
                Status = JobStatus.Scheduled,
                ScheduledStartAt = DateTime.UtcNow.AddMinutes(30),
                QueueName = QueueSettings.DefaultQueueName,
                SerializableGroupId = firstJobFromFirstGroup.SerializableGroupId,
                LockGroupIfFailed = false
            },
            
            jobWithoutGroup,
            firstJobFromFirstGroup,

            // should be skipped
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = "1_2",
                Cron = null,
                JobParam = "param2",
                StartedCount = 0,
                NextJobId = null,
                Status = JobStatus.Scheduled,
                ScheduledStartAt = DateTime.UtcNow.AddMinutes(-3),
                QueueName = QueueSettings.DefaultQueueName,
                SerializableGroupId = firstJobFromFirstGroup.SerializableGroupId
            },
            
            firstJobFromSecondGroup
        };
        
        dbContext.Jobs.AddRange(jobs);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        var result = new List<JobExecutionModel>();
        var request = new GetJobsRequest
        {
            QueueName = QueueSettings.DefaultQueueName,
            BatchSize = 3,
            ServerId = "serverId"
        };
        await storage.TakeBatchToProcessingAsync(request, result);

        Assert.Equal(3, result.Count);
        AssertTakenToRunJob(jobWithoutGroup, result[0], request.ServerId);
        AssertTakenToRunJob(firstJobFromFirstGroup, result[1], request.ServerId);
        AssertTakenToRunJob(firstJobFromSecondGroup, result[2], request.ServerId);
    }
    
    [Theory]
    [InlineData(JobStatus.Processing, 1, false)]
    [InlineData(JobStatus.Scheduled, 1, true)]
    [InlineData(JobStatus.Failed, 1, true)]
    public async Task TakeBatchToProcessingAsync_DoesNotReturnJobFromLockedGroup(JobStatus lockerStatus, 
        int lockerStartedCount, bool lockerLockIfFailed)
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();

        var jobs = new List<JobDbModel>
        {
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = "locker",
                Cron = null,
                JobParam = "param",
                StartedCount = lockerStartedCount,
                NextJobId = null,
                Status = lockerStatus,
                ScheduledStartAt = DateTime.UtcNow.AddMinutes(-3),
                QueueName = QueueSettings.DefaultQueueName,
                SerializableGroupId = "gid",
                LockGroupIfFailed = lockerLockIfFailed
            },
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = "locked",
                Cron = null,
                JobParam = "param",
                StartedCount = 0,
                NextJobId = null,
                Status = JobStatus.Scheduled,
                ScheduledStartAt = DateTime.UtcNow.AddMinutes(-3),
                QueueName = QueueSettings.DefaultQueueName,
                SerializableGroupId = "gid"
            },
        };
        
        dbContext.Jobs.AddRange(jobs);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        var result = new List<JobExecutionModel>();
        var request = new GetJobsRequest
        {
            QueueName = QueueSettings.DefaultQueueName,
            BatchSize = 10,
            ServerId = "serverId"
        };
        await storage.TakeBatchToProcessingAsync(request, result);

        Assert.Empty(result);
    }    

    private void AssertTakenToRunJob(JobDbModel createdJob, JobExecutionModel takenJob, string serverId)
    {
        Assert.Equal(createdJob.Id, takenJob.Id);
        Assert.Equal(createdJob.JobName, takenJob.JobName);
        Assert.Equal(createdJob.Cron, takenJob.Cron);
        Assert.Equal(createdJob.JobParam, takenJob.JobParam);
        Assert.Equal(createdJob.StartedCount + 1, takenJob.StartedCount);
        Assert.Equal(createdJob.NextJobId, takenJob.NextJobId);
        Assert.Equal(serverId, takenJob.ServerId);
    }

    private void AssertUpdatedFields(JobDbModel createdJob, JobDbModel actualJob)
    {
        Assert.Equal(createdJob.StartedCount + 1, actualJob.StartedCount);
        Assert.Equal(JobStatus.Processing, actualJob.Status);
        Assert.Equal("serverId", actualJob.ServerId);
        Assert.NotNull(actualJob.LastStartedAt);
        Assert.Equal(DateTime.UtcNow, actualJob.LastStartedAt.Value, TimeSpan.FromSeconds(3));
    }
}
