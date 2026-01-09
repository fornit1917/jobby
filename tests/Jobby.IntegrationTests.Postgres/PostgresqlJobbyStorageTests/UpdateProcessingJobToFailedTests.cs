using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class UpdateProcessingJobToFailedTests
{
    [Fact]
    public async Task UpdateProcessingJobToFailedAsync_Updates()
    {
        await using var dbContext = DbHelper.CreateContext();

        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            Cron = null,
            JobParam = "param",
            StartedCount = 1,
            NextJobId = null,
            Status = JobStatus.Processing,
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            ServerId = Guid.NewGuid().ToString(),
        };
        await dbContext.AddAsync(job);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        var failedReason = "some error message";
        await storage.UpdateProcessingJobToFailedAsync(job.ToProcessingJob(), failedReason);

        var actualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.Id);
        Assert.Equal(JobStatus.Failed, actualJob.Status);
        Assert.NotNull(actualJob.LastFinishedAt);
        Assert.Equal(DateTime.UtcNow, actualJob.LastFinishedAt.Value, TimeSpan.FromSeconds(3));
        Assert.Equal(failedReason, actualJob.Error);
    }

    [Fact]
    public async Task UpdateProcessingJobToFailedAsync_NoNext_NotInProcessingStatus_DoesNothing()
    {
        await using var dbContext = DbHelper.CreateContext();

        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            Cron = null,
            JobParam = "param",
            StartedCount = 1,
            NextJobId = null,
            Status = JobStatus.Completed,
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            ServerId = Guid.NewGuid().ToString(),
        };
        await dbContext.AddAsync(job);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        var failedReason = "some error message";
        await storage.UpdateProcessingJobToFailedAsync(job.ToProcessingJob(), failedReason);

        var actualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.Id);
        Assert.Equal(JobStatus.Completed, actualJob.Status);
    }

    [Fact]
    public async Task UpdateProcessingJobToFailedAsync_NoNext_OnOtherServer_DoesNothing()
    {
        await using var dbContext = DbHelper.CreateContext();

        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            Cron = null,
            JobParam = "param",
            StartedCount = 1,
            NextJobId = null,
            Status = JobStatus.Processing,
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            ServerId = "new_server",
        };
        await dbContext.AddAsync(job);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        var failedReason = "some error message";
        await storage.UpdateProcessingJobToFailedAsync(new ProcessingJob(job.Id, "old_server"), failedReason);

        var actualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.Id);
        Assert.Equal(JobStatus.Processing, actualJob.Status);
    }    
}