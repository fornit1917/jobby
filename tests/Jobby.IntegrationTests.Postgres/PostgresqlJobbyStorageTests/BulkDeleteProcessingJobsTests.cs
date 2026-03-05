using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class BulkDeleteProcessingJobsTests
{
    [Fact]
    public async Task BulkDeleteProcessingJobsAsync_NoNextJobs_Deletes()
    {
        var serverId = Guid.NewGuid().ToString();
        var jobs = new List<JobDbModel>
        {
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                JobParam = "param",
                Status = JobStatus.Processing,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
                ServerId = serverId,
            },
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                JobParam = "param",
                Status = JobStatus.Processing,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
                ServerId = serverId,
            },
        };

        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync(jobs, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateJobbyStorage();
        await storage.BulkDeleteProcessingJobsAsync(jobs.ToCompleteJobsBatch(serverId));

        var actualJobs = await dbContext.Jobs.AsNoTracking()
            .Where(x => x.Id == jobs[0].Id || x.Id == jobs[1].Id)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(actualJobs);
    }

    [Fact]
    public async Task BulkDeleteProcessingJobsAsync_HaveNextJobs_DeletesCurrentAndSchedulesNext()
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

        var serverId = Guid.NewGuid().ToString();
        var jobs = new List<JobDbModel>
        {
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                JobParam = "param",
                NextJobId = nextJobs[0].Id,
                Status = JobStatus.Processing,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
                ServerId = serverId,
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
                ServerId = serverId,
            },
        };

        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync(jobs.Concat(nextJobs), TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateJobbyStorage();
        await storage.BulkDeleteProcessingJobsAsync(jobs.ToCompleteJobsBatch(serverId));

        var actualJobs = await dbContext.Jobs.AsNoTracking()
            .Where(x => x.Id == jobs[0].Id || x.Id == jobs[1].Id)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(actualJobs);

        var firstActualNextJob = await dbContext.Jobs.AsNoTracking()
            .FirstAsync(x => x.Id == nextJobs[0].Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Scheduled, firstActualNextJob.Status);
        var secondActualNextJob = await dbContext.Jobs.AsNoTracking()
            .FirstAsync(x => x.Id == nextJobs[1].Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Scheduled, secondActualNextJob.Status);
    }

    [Fact]
    public async Task BulkDeleteProcessingJobsAsync_CurrentNotProcessing_DoesNothing()
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

        var serverId = Guid.NewGuid().ToString();
        var jobs = new List<JobDbModel>
        {
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                JobParam = "param",
                NextJobId = nextJobs[0].Id,
                Status = JobStatus.Completed,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
                ServerId = serverId,
            },
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                JobParam = "param",
                StartedCount = 2,
                NextJobId = nextJobs[1].Id,
                Status = JobStatus.Completed,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
                ServerId = serverId,
            },
        };

        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync(jobs.Concat(nextJobs), TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateJobbyStorage();
        await storage.BulkDeleteProcessingJobsAsync(jobs.ToCompleteJobsBatch(serverId));

        var actualJobs = await dbContext.Jobs.AsNoTracking()
            .Where(x => x.Id == jobs[0].Id || x.Id == jobs[1].Id)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, actualJobs.Count);
        Assert.Contains(actualJobs, x => x.Id == jobs[0].Id && x.Status == JobStatus.Completed);
        Assert.Contains(actualJobs, x => x.Id == jobs[1].Id && x.Status == JobStatus.Completed);

        var firstActualNextJob = await dbContext.Jobs.AsNoTracking()
            .FirstAsync(x => x.Id == nextJobs[0].Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.WaitingPrev, firstActualNextJob.Status);
        var secondActualNextJob = await dbContext.Jobs.AsNoTracking()
            .FirstAsync(x => x.Id == nextJobs[1].Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.WaitingPrev, secondActualNextJob.Status);
    }

    [Fact]
    public async Task BulkDeleteProcessingJobsAsync_CurrentOnOtherServer_DoesNothing()
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
                NextJobId = nextJobs[0].Id,
                Status = JobStatus.Processing,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
                ServerId = "new_server",
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
                ServerId = "new_server",
            },
        };

        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync(jobs.Concat(nextJobs), TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateJobbyStorage();
        await storage.BulkDeleteProcessingJobsAsync(jobs.ToCompleteJobsBatch("old_server"));

        var actualJobs = await dbContext.Jobs.AsNoTracking()
            .Where(x => x.Id == jobs[0].Id || x.Id == jobs[1].Id)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, actualJobs.Count);
        Assert.Contains(actualJobs, x => x.Id == jobs[0].Id && x.Status == JobStatus.Processing);
        Assert.Contains(actualJobs, x => x.Id == jobs[1].Id && x.Status == JobStatus.Processing);

        var firstActualNextJob = await dbContext.Jobs.AsNoTracking()
            .FirstAsync(x => x.Id == nextJobs[0].Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.WaitingPrev, firstActualNextJob.Status);
        var secondActualNextJob = await dbContext.Jobs.AsNoTracking()
            .FirstAsync(x => x.Id == nextJobs[1].Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.WaitingPrev, secondActualNextJob.Status);
    }
}