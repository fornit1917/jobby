using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection("Jobby.Postgres.IntegrationTests")]
public class InsertMethodsTests
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
        await storage.InsertAsync(firstJob);
        await storage.InsertAsync(secondJob);

        var firstActualJob = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == firstJob.Id);
        var secondActualJob = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == secondJob.Id);

        Assert.NotNull(firstActualJob);
        Assert.NotNull(secondActualJob);
        AssertCreatedJob(firstJob, firstActualJob);
        AssertCreatedJob(secondJob, secondActualJob);
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

        storage.Insert(firstJob);
        storage.Insert(secondJob);

        var firstActualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == firstJob.Id);
        var secondActualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == secondJob.Id);

        Assert.NotNull(firstActualJob);
        Assert.NotNull(secondActualJob);
        AssertCreatedJob(firstJob, firstActualJob);
        AssertCreatedJob(secondJob, secondActualJob);
    }

    [Fact]
    public async Task InsertsAsync_RecurrentExisting_UpdatesRecurrent()
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
        await storage.InsertAsync(job);
        await storage.InsertAsync(newJob);

        var actualJobWithOldId = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == job.Id);
        Assert.Null(actualJobWithOldId);
        var actualJobWithNewId = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == newJob.Id);
        Assert.NotNull(actualJobWithNewId);
        AssertCreatedJob(newJob, actualJobWithNewId);
    }

    [Fact]
    public void Inserts_RecurrentExisting_UpdatesRecurrent()
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
        storage.Insert(job);
        storage.Insert(newJob);

        var actualJobWithOldId = dbContext.Jobs.FirstOrDefault(x => x.Id == job.Id);
        Assert.Null(actualJobWithOldId);
        var actualJobWithNewId = dbContext.Jobs.FirstOrDefault(x => x.Id == newJob.Id);
        Assert.NotNull(actualJobWithNewId);
        AssertCreatedJob(newJob, actualJobWithNewId);
    }

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

        await storage.BulkInsertAsync([firstJob, secondJob]);

        var firstActualJob = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == firstJob.Id);
        var secondActualJob = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == secondJob.Id);

        Assert.NotNull(firstActualJob);
        Assert.NotNull(secondActualJob);
        AssertCreatedJob(firstJob, firstActualJob);
        AssertCreatedJob(secondJob, secondActualJob);
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
        storage.BulkInsert([firstJob, secondJob]);

        var firstActualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == firstJob.Id);
        var secondActualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == secondJob.Id);

        Assert.NotNull(firstActualJob);
        Assert.NotNull(secondActualJob);
        AssertCreatedJob(firstJob, firstActualJob);
        AssertCreatedJob(secondJob, secondActualJob);
    }

    private void AssertCreatedJob(JobCreationModel expected, JobDbModel actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Cron, actual.Cron);
        Assert.Equal(expected.CreatedAt, actual.CreatedAt, TimeSpan.FromSeconds(1));
        Assert.Equal(expected.JobName, actual.JobName);
        Assert.Equal(expected.CanBeRestarted, actual.CanBeRestarted);
        Assert.Equal(expected.NextJobId, actual.NextJobId);
        Assert.Equal(expected.ScheduledStartAt, actual.ScheduledStartAt, TimeSpan.FromSeconds(1));
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.JobParam, actual.JobParam);
        Assert.Null(actual.Error);
        Assert.Null(actual.LastStartedAt);
        Assert.Null(actual.LastFinishedAt);
        Assert.Equal(0, actual.StartedCount);
        Assert.Null(actual.ServerId);
    }
}
