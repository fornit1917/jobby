using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class BulkUpdateProcessingJobsToCompletedTests
{
    [Fact]
    public async Task BulkUpdateProcessingJobsToCompletedAsync_NoNextJobs_Completes()
    {
        var serverId = Guid.NewGuid().ToString();
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
                ServerId = serverId,
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
                ServerId = serverId,
                Error = "prev error"
            },
        };

        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync(jobs);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        var jobsToUpdate = new ProcessingJobsList(jobs.Select(x => x.Id).ToList(), serverId);
        await storage.BulkUpdateProcessingJobsToCompletedAsync(jobsToUpdate, Array.Empty<Guid>());

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
    public async Task BulkUpdateProcessingJobsToCompletedAsync_HaveNextJobs_CompletesCurrentAndSchedulesNext()
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
                StartedCount = 2,
                NextJobId = nextJobs[0].Id,
                Status = JobStatus.Processing,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
                ServerId = serverId,
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
                ServerId = serverId,
                Error = "prev error"
            },
        };

        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync(jobs.Concat(nextJobs));
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        var jobsToUpdate = new ProcessingJobsList(jobs.Select(x => x.Id).ToList(), serverId);
        await storage.BulkUpdateProcessingJobsToCompletedAsync(jobsToUpdate, nextJobs.Select(x => x.Id).ToList());

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

    [Fact]
    public async Task BulkUpdateProcessingJobsToCompletedAsync_CurrentNotProcessing_DoesNothing()
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
                StartedCount = 2,
                NextJobId = nextJobs[0].Id,
                Status = JobStatus.Failed,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
                ServerId = serverId,
                Error = "prev error"
            },
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                JobParam = "param",
                StartedCount = 2,
                NextJobId = nextJobs[1].Id,
                Status = JobStatus.Failed,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
                ServerId = serverId,
                Error = "prev error"
            },
        };

        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync(jobs.Concat(nextJobs));
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        var jobsToUpdate = new ProcessingJobsList(jobs.Select(x => x.Id).ToList(), serverId);
        await storage.BulkUpdateProcessingJobsToCompletedAsync(jobsToUpdate, nextJobs.Select(x => x.Id).ToList());

        var firstActualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == jobs[0].Id);
        Assert.Equal(JobStatus.Failed, firstActualJob.Status);
        
        var secondActualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == jobs[1].Id);
        Assert.Equal(JobStatus.Failed, secondActualJob.Status);

        var firstActualNextJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == nextJobs[0].Id);
        Assert.Equal(JobStatus.WaitingPrev, firstActualNextJob.Status);

        var secondActualNextJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == nextJobs[1].Id);
        Assert.Equal(JobStatus.WaitingPrev, secondActualNextJob.Status);
    }

    [Fact]
    public async Task BulkUpdateProcessingJobsToCompletedAsync_CurrentOnOtherServer_DoesNothing()
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
                ServerId = "new_server",
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
                ServerId = "new_server",
                Error = "prev error"
            },
        };

        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync(jobs.Concat(nextJobs));
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        var jobsToUpdate = new ProcessingJobsList(jobs.Select(x => x.Id).ToList(), "old_server");
        await storage.BulkUpdateProcessingJobsToCompletedAsync(jobsToUpdate, nextJobs.Select(x => x.Id).ToList());

        var firstActualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == jobs[0].Id);
        Assert.Equal(JobStatus.Processing, firstActualJob.Status);

        var secondActualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == jobs[1].Id);
        Assert.Equal(JobStatus.Processing, secondActualJob.Status);

        var firstActualNextJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == nextJobs[0].Id);
        Assert.Equal(JobStatus.WaitingPrev, firstActualNextJob.Status);

        var secondActualNextJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == nextJobs[1].Id);
        Assert.Equal(JobStatus.WaitingPrev, secondActualNextJob.Status);
    }    
}