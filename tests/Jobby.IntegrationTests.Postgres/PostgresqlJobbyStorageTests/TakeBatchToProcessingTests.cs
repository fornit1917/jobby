using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection("Jobby.Postgres.IntegrationTests")]
public class TakeBatchToProcessingTests
{
    [Fact]
    public async Task TakeBatchToProcessingAsync_ReturnsReadyToRunAndUpdatesStatusAndCount()
    {
        await using var dbContext = DbHelper.CreateContextAndClearDb();

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
        };
        var jobs = new List<JobDbModel>
        {
            firstExpected,
            secondExpected,

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
        await storage.TakeBatchToProcessingAsync("serverId", 2, result);

        Assert.Equal(2, result.Count);
        AssertTakenToRunJob(firstExpected, result[0]);
        AssertTakenToRunJob(secondExpected, result[1]);

        var firstActualFromDb = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == firstExpected.Id);
        AssertUpdatedFields(firstExpected, firstActualFromDb);
        var secondActualFromDb = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == secondExpected.Id);
        AssertUpdatedFields(secondExpected, secondActualFromDb);

        await dbContext.SaveChangesAsync();
    }

    private void AssertTakenToRunJob(JobDbModel createdJob, JobExecutionModel takenJob)
    {
        Assert.Equal(createdJob.Id, takenJob.Id);
        Assert.Equal(createdJob.JobName, takenJob.JobName);
        Assert.Equal(createdJob.Cron, takenJob.Cron);
        Assert.Equal(createdJob.JobParam, takenJob.JobParam);
        Assert.Equal(createdJob.StartedCount + 1, takenJob.StartedCount);
        Assert.Equal(createdJob.NextJobId, takenJob.NextJobId);
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
