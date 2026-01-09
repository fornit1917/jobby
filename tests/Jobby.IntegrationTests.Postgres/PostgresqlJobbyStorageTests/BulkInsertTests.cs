using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class BulkInsertTests
{
    [Fact]
    public async Task BulkInsertAsync_Inserts()
    {
        await using var dbContext = DbHelper.CreateContext();

        var firstJob = new JobCreationModel
        {
            Id = Guid.NewGuid(),
            Cron = "*/10 * * * *",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            JobName = Guid.NewGuid().ToString(),
            CanBeRestarted = true,
            NextJobId = Guid.NewGuid(),
            ScheduledStartAt = DateTime.UtcNow.AddDays(100),
            Status = JobStatus.Scheduled,
            JobParam = "param1"
        };

        var secondJob = new JobCreationModel
        {
            Id = Guid.NewGuid(),
            Cron = null,
            CreatedAt = DateTime.UtcNow.AddMinutes(-20),
            JobName = Guid.NewGuid().ToString(),
            CanBeRestarted = false,
            NextJobId = null,
            ScheduledStartAt = DateTime.UtcNow.AddDays(200),
            Status = JobStatus.Failed,
            JobParam = "param2"
        };

        var storage = DbHelper.CreateJobbyStorage();

        await storage.BulkInsertJobsAsync([firstJob, secondJob]);

        var firstActualJob = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == firstJob.Id);
        var secondActualJob = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == secondJob.Id);

        Assert.NotNull(firstActualJob);
        Assert.NotNull(secondActualJob);
        AssertHelper.AssertCreatedJob(firstJob, firstActualJob);
        AssertHelper.AssertCreatedJob(secondJob, secondActualJob);
    }

    [Fact]
    public void BulkInsert_Inserts()
    {
        using var dbContext = DbHelper.CreateContext();

        var firstJob = new JobCreationModel
        {
            Id = Guid.NewGuid(),
            Cron = "*/10 * * * *",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            JobName = Guid.NewGuid().ToString(),
            CanBeRestarted = true,
            NextJobId = Guid.NewGuid(),
            ScheduledStartAt = DateTime.UtcNow.AddDays(100),
            Status = JobStatus.Scheduled,
            JobParam = "param1"
        };

        var secondJob = new JobCreationModel
        {
            Id = Guid.NewGuid(),
            Cron = null,
            CreatedAt = DateTime.UtcNow.AddMinutes(-20),
            JobName = Guid.NewGuid().ToString(),
            CanBeRestarted = false,
            NextJobId = null,
            ScheduledStartAt = DateTime.UtcNow.AddDays(200),
            Status = JobStatus.Failed,
            JobParam = "param2"
        };

        var storage = DbHelper.CreateJobbyStorage();
        storage.BulkInsertJobs([firstJob, secondJob]);

        var firstActualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == firstJob.Id);
        var secondActualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == secondJob.Id);

        Assert.NotNull(firstActualJob);
        Assert.NotNull(secondActualJob);
        AssertHelper.AssertCreatedJob(firstJob, firstActualJob);
        AssertHelper.AssertCreatedJob(secondJob, secondActualJob);
    }    
}