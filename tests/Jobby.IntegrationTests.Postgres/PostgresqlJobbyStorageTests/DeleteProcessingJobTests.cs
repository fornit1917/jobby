using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class DeleteProcessingJobTests
{
    [Fact]
    public async Task DeleteProcessingJobAsync_NoNextJob_Deletes()
    {
        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            Status = JobStatus.Processing,
            ServerId = Guid.NewGuid().ToString(),
        };
        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddAsync(job);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.DeleteProcessingJobAsync(job.ToProcessingJob());

        var jobExists = await dbContext.Jobs.AsNoTracking().Where(x => x.Id == job.Id).AnyAsync();
        Assert.False(jobExists);
    }

    [Fact]
    public async Task DeleteProcessingJobAsync_HasNextJob_DeletesCurrentAndSchedulesNext()
    {
        var nextJob = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            Status = JobStatus.WaitingPrev,
        };
        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            NextJobId = nextJob.Id,
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            Status = JobStatus.Processing,
            ServerId = Guid.NewGuid().ToString(),
        };
        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync([job, nextJob]);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.DeleteProcessingJobAsync(job.ToProcessingJob(), job.NextJobId);

        var jobExists = await dbContext.Jobs.AsNoTracking().Where(x => x.Id == job.Id).AnyAsync();
        Assert.False(jobExists);

        var actualNextJob = await dbContext.Jobs.AsNoTracking().Where(x => x.Id == nextJob.Id).FirstOrDefaultAsync();
        Assert.NotNull(actualNextJob);
        Assert.Equal(JobStatus.Scheduled, actualNextJob.Status);
    }

    [Fact]
    public async Task DeleteProcessingJobAsync_CurrentNotProcessing_DoesNothing()
    {
        var nextJob = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            Status = JobStatus.WaitingPrev,
        };
        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            NextJobId = nextJob.Id,
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            Status = JobStatus.Completed,
            ServerId = Guid.NewGuid().ToString(),
        };
        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync([job, nextJob]);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.DeleteProcessingJobAsync(job.ToProcessingJob(), job.NextJobId);

        var jobExists = await dbContext.Jobs.AsNoTracking().Where(x => x.Id == job.Id).AnyAsync();
        Assert.True(jobExists);

        var actualNextJob = await dbContext.Jobs.AsNoTracking().Where(x => x.Id == nextJob.Id).FirstOrDefaultAsync();
        Assert.NotNull(actualNextJob);
        Assert.Equal(JobStatus.WaitingPrev, actualNextJob.Status);
    }

    [Fact]
    public async Task DeleteProcessingJobAsync_CurrentOnOtherService_DoesNothing()
    {
        var nextJob = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            Status = JobStatus.WaitingPrev,
        };
        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            NextJobId = nextJob.Id,
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            Status = JobStatus.Processing,
            ServerId = "new_server",
        };
        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync([job, nextJob]);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.DeleteProcessingJobAsync(new ProcessingJob(job.Id, "old_server"), job.NextJobId);

        var jobExists = await dbContext.Jobs.AsNoTracking().Where(x => x.Id == job.Id).AnyAsync();
        Assert.True(jobExists);

        var actualNextJob = await dbContext.Jobs.AsNoTracking().Where(x => x.Id == nextJob.Id).FirstOrDefaultAsync();
        Assert.NotNull(actualNextJob);
        Assert.Equal(JobStatus.WaitingPrev, actualNextJob.Status);
    }    
}