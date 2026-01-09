using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class InsertTests
{
    [Fact]
    public async Task InsertAsync_Inserts()
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
        await storage.InsertJobAsync(firstJob);
        await storage.InsertJobAsync(secondJob);

        var firstActualJob = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == firstJob.Id);
        var secondActualJob = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == secondJob.Id);

        Assert.NotNull(firstActualJob);
        Assert.NotNull(secondActualJob);
        AssertHelper.AssertCreatedJob(firstJob, firstActualJob);
        AssertHelper.AssertCreatedJob(secondJob, secondActualJob);
    }

    [Fact]
    public void Insert_Inserts()
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

        storage.InsertJob(firstJob);
        storage.InsertJob(secondJob);

        var firstActualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == firstJob.Id);
        var secondActualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == secondJob.Id);

        Assert.NotNull(firstActualJob);
        Assert.NotNull(secondActualJob);
        AssertHelper.AssertCreatedJob(firstJob, firstActualJob);
        AssertHelper.AssertCreatedJob(secondJob, secondActualJob);
    }

    [Fact]
    public async Task InsertAsync_RecurrentExisting_UpdatesRecurrent()
    {
        await using var dbContext = DbHelper.CreateContext();
        var jobName = Guid.NewGuid().ToString();
        var job = new JobCreationModel
        {
            Id = Guid.NewGuid(),
            Cron = "*/10 * * * *",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            JobName = jobName,
            CanBeRestarted = true,
            ScheduledStartAt = DateTime.UtcNow.AddDays(100),
            Status = JobStatus.Scheduled,
            JobParam = "param1"
        };
        var newJob = new JobCreationModel
        {
            Id = Guid.NewGuid(),
            Cron = "*/5 * * * *",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            JobName = jobName,
            CanBeRestarted = false,
            ScheduledStartAt = DateTime.UtcNow.AddDays(200),
            Status = JobStatus.Scheduled,
            JobParam = "param2"
        };

        var storage = DbHelper.CreateJobbyStorage();
        await storage.InsertJobAsync(job);
        await storage.InsertJobAsync(newJob);

        var actualJobWithOldId = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == job.Id);
        Assert.Null(actualJobWithOldId);
        var actualJobWithNewId = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == newJob.Id);
        Assert.NotNull(actualJobWithNewId);
        AssertHelper.AssertCreatedJob(newJob, actualJobWithNewId);
    }

    [Fact]
    public void Insert_RecurrentExisting_UpdatesRecurrent()
    {
        using var dbContext = DbHelper.CreateContext();
        var jobName = Guid.NewGuid().ToString();
        var job = new JobCreationModel
        {
            Id = Guid.NewGuid(),
            Cron = "*/10 * * * *",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            JobName = jobName,
            CanBeRestarted = true,
            ScheduledStartAt = DateTime.UtcNow.AddDays(100),
            Status = JobStatus.Scheduled,
            JobParam = "param1"
        };
        var newJob = new JobCreationModel
        {
            Id = Guid.NewGuid(),
            Cron = "*/5 * * * *",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            JobName = jobName,
            CanBeRestarted = false,
            ScheduledStartAt = DateTime.UtcNow.AddDays(200),
            Status = JobStatus.Scheduled,
            JobParam = "param2"
        };

        var storage = DbHelper.CreateJobbyStorage();
        storage.InsertJob(job);
        storage.InsertJob(newJob);

        var actualJobWithOldId = dbContext.Jobs.FirstOrDefault(x => x.Id == job.Id);
        Assert.Null(actualJobWithOldId);
        var actualJobWithNewId = dbContext.Jobs.FirstOrDefault(x => x.Id == newJob.Id);
        Assert.NotNull(actualJobWithNewId);
        AssertHelper.AssertCreatedJob(newJob, actualJobWithNewId);
    }    
}