using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class RescheduleProcessingJobTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("some error message")]
    public async Task RescheduleProcessingJobAsync_Reschedules(string error)
    {
        await using var dbContext = DbHelper.CreateContext();

        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            Cron = "*/5 * * * *",
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
        var newStartTime = DateTime.UtcNow.AddDays(2);
        await storage.RescheduleProcessingJobAsync(job.ToProcessingJob(), newStartTime, error);

        var actualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.Id);
        Assert.Equal(JobStatus.Scheduled, actualJob.Status);
        Assert.Equal(newStartTime, actualJob.ScheduledStartAt, TimeSpan.FromSeconds(1));
        Assert.NotNull(actualJob.LastFinishedAt);
        Assert.Equal(DateTime.UtcNow, actualJob.LastFinishedAt.Value, TimeSpan.FromSeconds(3));
        Assert.Equal(error, actualJob.Error);
    }

    [Fact]
    public async Task RescheduleProcessingJobAsync_NotInProcessingStatus_DoesNothing()
    {
        await using var dbContext = DbHelper.CreateContext();

        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
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
        var newStartTime = DateTime.UtcNow.AddDays(2);
        await storage.RescheduleProcessingJobAsync(job.ToProcessingJob(), newStartTime, null);

        var actualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.Id);
        Assert.Equal(JobStatus.Completed, actualJob.Status);
        Assert.Equal(job.ScheduledStartAt, actualJob.ScheduledStartAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RescheduleProcessingJobAsync_OnOtherServer_DoesNothing()
    {
        await using var dbContext = DbHelper.CreateContext();

        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            StartedCount = 1,
            NextJobId = null,
            Status = JobStatus.Processing,
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            ServerId = "new_sever",
        };
        await dbContext.AddAsync(job);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        var newStartTime = DateTime.UtcNow.AddDays(2);
        await storage.RescheduleProcessingJobAsync(new ProcessingJob(job.Id, "old_server"), newStartTime, null);

        var actualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.Id);
        Assert.Equal(JobStatus.Processing, actualJob.Status);
        Assert.Equal(job.ScheduledStartAt, actualJob.ScheduledStartAt, TimeSpan.FromSeconds(1));
    }    
}