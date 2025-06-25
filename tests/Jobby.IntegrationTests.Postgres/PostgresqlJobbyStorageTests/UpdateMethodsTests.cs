using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection("Jobby.Postgres.IntegrationTests")]
public class UpdateMethodsTests
{
    [Fact]
    public async Task MarkFailedAsync_MarksFailed()
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
        };
        await dbContext.AddAsync(job);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        var failedReason = "some error message";
        await storage.MarkFailedAsync(job.Id, failedReason);

        var actualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.Id);
        Assert.Equal(JobStatus.Failed, actualJob.Status);
        Assert.NotNull(actualJob.LastFinishedAt);
        Assert.Equal(DateTime.UtcNow, actualJob.LastFinishedAt.Value, TimeSpan.FromSeconds(3));
        Assert.Equal(failedReason, actualJob.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("some error message")]
    public async Task RescheduleAsync_Reschedules(string error)
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
        };
        await dbContext.AddAsync(job);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        var newStartTime = DateTime.UtcNow.AddDays(2);
        await storage.RescheduleAsync(job.Id, newStartTime, error);

        var actualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.Id);
        Assert.Equal(JobStatus.Scheduled, actualJob.Status);
        Assert.Equal(newStartTime, actualJob.ScheduledStartAt, TimeSpan.FromSeconds(1));
        Assert.NotNull(actualJob.LastFinishedAt);
        Assert.Equal(DateTime.UtcNow, actualJob.LastFinishedAt.Value, TimeSpan.FromSeconds(3));
        Assert.Equal(error, actualJob.Error);
    }

    [Fact]
    public async Task MarkCompletedAsync_NoNextJob_CompletesCurrent()
    {
        await using var dbContext = DbHelper.CreateContext();

        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            Cron = null,
            JobParam = "param",
            StartedCount = 2,
            NextJobId = null,
            Status = JobStatus.Processing,
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            Error = "prev error"
        };
        await dbContext.AddAsync(job);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.MarkCompletedAsync(job.Id);

        var actualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.Id);
        Assert.Equal(JobStatus.Completed, actualJob.Status);
        Assert.NotNull(actualJob.LastFinishedAt);
        Assert.Equal(DateTime.UtcNow, actualJob.LastFinishedAt.Value, TimeSpan.FromSeconds(3));
        Assert.Null(actualJob.Error);
    }

    [Fact]
    public async Task MarkCompletedAsync_HasNextJob_CompletesCurrentAndSchedulesNext()
    {
        await using var dbContext = DbHelper.CreateContext();
        var nextJob = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            Status = JobStatus.WaitingPrev,
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
        };
        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            StartedCount = 2,
            NextJobId = nextJob.Id,
            Status = JobStatus.Processing,
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            Error = "prev error"
        };
        await dbContext.AddRangeAsync([nextJob, job]);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.MarkCompletedAsync(job.Id, job.NextJobId);

        var actualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.Id);
        Assert.Equal(JobStatus.Completed, actualJob.Status);
        Assert.NotNull(actualJob.LastFinishedAt);
        Assert.Equal(DateTime.UtcNow, actualJob.LastFinishedAt.Value, TimeSpan.FromSeconds(3));
        Assert.Null(actualJob.Error);

        var actualNextJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.NextJobId);
        Assert.Equal(JobStatus.Scheduled, actualNextJob.Status);
    }

    [Fact]
    public async Task BulkMarkCompletedAsync_NoNextJobs_Completes()
    {
        var jobs = new List<JobDbModel>
        {
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                JobParam = "param",
                StartedCount = 2,
                Status = JobStatus.Processing,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
                Error = "prev error"
            },
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                JobParam = "param",
                StartedCount = 2,
                Status = JobStatus.Processing,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
                Error = "prev error"
            },
        };

        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync(jobs);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.BulkMarkCompletedAsync(jobs.Select(x => x.Id).ToList(), Array.Empty<Guid>());

        var firstActualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == jobs[0].Id);
        Assert.Equal(JobStatus.Completed, firstActualJob.Status);
        Assert.NotNull(firstActualJob.LastFinishedAt);
        Assert.Equal(DateTime.UtcNow, firstActualJob.LastFinishedAt.Value, TimeSpan.FromSeconds(3));
        Assert.Null(firstActualJob.Error);

        var secondActualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == jobs[1].Id);
        Assert.Equal(JobStatus.Completed, secondActualJob.Status);
        Assert.NotNull(secondActualJob.LastFinishedAt);
        Assert.Equal(DateTime.UtcNow, secondActualJob.LastFinishedAt.Value, TimeSpan.FromSeconds(3));
        Assert.Null(secondActualJob.Error);
    }

    [Fact]
    public async Task BulkMarkCompletedAsync_HaveNextJobs_CompletesCurrentAndSchedulesNext()
    {
        var nextJobs = new List<JobDbModel>
        {
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                JobParam = "param",
                Status = JobStatus.WaitingPrev,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            },
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                JobParam = "param",
                Status = JobStatus.WaitingPrev,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            },
        };

        var jobs = new List<JobDbModel>
        {
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                JobParam = "param",
                StartedCount = 2,
                NextJobId = nextJobs[0].Id,
                Status = JobStatus.Processing,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
                Error = "prev error"
            },
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                JobParam = "param",
                StartedCount = 2,
                NextJobId = nextJobs[1].Id,
                Status = JobStatus.Processing,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
                Error = "prev error"
            },
        };

        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync(jobs.Concat(nextJobs));
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.BulkMarkCompletedAsync(jobs.Select(x => x.Id).ToList(), nextJobs.Select(x => x.Id).ToList());

        var firstActualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == jobs[0].Id);
        Assert.Equal(JobStatus.Completed, firstActualJob.Status);
        Assert.NotNull(firstActualJob.LastFinishedAt);
        Assert.Equal(DateTime.UtcNow, firstActualJob.LastFinishedAt.Value, TimeSpan.FromSeconds(3));
        Assert.Null(firstActualJob.Error);

        var secondActualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == jobs[1].Id);
        Assert.Equal(JobStatus.Completed, secondActualJob.Status);
        Assert.NotNull(secondActualJob.LastFinishedAt);
        Assert.Equal(DateTime.UtcNow, secondActualJob.LastFinishedAt.Value, TimeSpan.FromSeconds(3));
        Assert.Null(secondActualJob.Error);

        var firstActualNextJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == nextJobs[0].Id);
        Assert.Equal(JobStatus.Scheduled, firstActualNextJob.Status);

        var secondActualNextJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == nextJobs[1].Id);
        Assert.Equal(JobStatus.Scheduled, secondActualNextJob.Status);
    }
}
