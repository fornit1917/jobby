using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection("Jobby.Postgres.IntegrationTests")]
public class DeleteMethodsTests
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
        await dbContext.AddRangeAsync(jobs);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        var processingJobs = new ProcessingJobsList(jobs.Select(x => x.Id).ToList(), serverId);
        await storage.BulkDeleteProcessingJobsAsync(processingJobs);

        var actualJobs = await dbContext.Jobs.AsNoTracking()
            .Where(x => x.Id == jobs[0].Id || x.Id == jobs[1].Id).ToListAsync();
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
        await dbContext.AddRangeAsync(jobs.Concat(nextJobs));
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        var processingJobs = new ProcessingJobsList(jobs.Select(x => x.Id).ToList(), serverId);
        await storage.BulkDeleteProcessingJobsAsync(processingJobs, nextJobs.Select(x => x.Id).ToList());

        var actualJobs = await dbContext.Jobs.AsNoTracking()
            .Where(x => x.Id == jobs[0].Id || x.Id == jobs[1].Id).ToListAsync();
        Assert.Empty(actualJobs);

        var firstActualNextJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == nextJobs[0].Id);
        Assert.Equal(JobStatus.Scheduled, firstActualNextJob.Status);
        var secondActualNextJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == nextJobs[1].Id);
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
        await dbContext.AddRangeAsync(jobs.Concat(nextJobs));
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        var jobsToDelete = new ProcessingJobsList(jobs.Select(x => x.Id).ToList(), serverId);
        await storage.BulkDeleteProcessingJobsAsync(jobsToDelete, nextJobs.Select(x => x.Id).ToList());

        var actualJobs = await dbContext.Jobs.AsNoTracking()
            .Where(x => x.Id == jobs[0].Id || x.Id == jobs[1].Id).ToListAsync();

        Assert.Equal(2, actualJobs.Count);
        Assert.Contains(actualJobs, x => x.Id == jobs[0].Id && x.Status == JobStatus.Completed);
        Assert.Contains(actualJobs, x => x.Id == jobs[1].Id && x.Status == JobStatus.Completed);

        var firstActualNextJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == nextJobs[0].Id);
        Assert.Equal(JobStatus.WaitingPrev, firstActualNextJob.Status);
        var secondActualNextJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == nextJobs[1].Id);
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
        await dbContext.AddRangeAsync(jobs.Concat(nextJobs));
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        var jobsToDelete = new ProcessingJobsList(jobs.Select(x => x.Id).ToList(), "old_server");
        await storage.BulkDeleteProcessingJobsAsync(jobsToDelete, nextJobs.Select(x => x.Id).ToList());

        var actualJobs = await dbContext.Jobs.AsNoTracking()
            .Where(x => x.Id == jobs[0].Id || x.Id == jobs[1].Id).ToListAsync();

        Assert.Equal(2, actualJobs.Count);
        Assert.Contains(actualJobs, x => x.Id == jobs[0].Id && x.Status == JobStatus.Processing);
        Assert.Contains(actualJobs, x => x.Id == jobs[1].Id && x.Status == JobStatus.Processing);

        var firstActualNextJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == nextJobs[0].Id);
        Assert.Equal(JobStatus.WaitingPrev, firstActualNextJob.Status);
        var secondActualNextJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == nextJobs[1].Id);
        Assert.Equal(JobStatus.WaitingPrev, secondActualNextJob.Status);
    }
    [Fact]
    public async Task BulkDeleteNotStartedJobsAsync_NotStartedStatuses_Deletes()
    {
        var jobs = new List<JobDbModel>
        {
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                JobParam = "param",
                Status = JobStatus.Scheduled,
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

        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync(jobs);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.BulkDeleteNotStartedJobsAsync(jobs.Select(x => x.Id).ToList());

        var actualJobs = await dbContext.Jobs.AsNoTracking()
            .Where(x => x.Id == jobs[0].Id || x.Id == jobs[1].Id).ToListAsync();
        Assert.Empty(actualJobs);
    }

    [Fact]
    public async Task BulkDeleteNotStartedJobsAsync_AlreadyStarted_DoesNothing()
    {
        var jobs = new List<JobDbModel>
        {
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                JobParam = "param",
                Status = JobStatus.Processing,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            },
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                JobParam = "param",
                Status = JobStatus.Completed,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            },
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                JobParam = "param",
                Status = JobStatus.Failed,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            },
        };

        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync(jobs);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.BulkDeleteNotStartedJobsAsync(jobs.Select(x => x.Id).ToList());

        var actualJobs = await dbContext.Jobs.AsNoTracking()
            .Where(x => x.Id == jobs[0].Id || x.Id == jobs[1].Id || x.Id == jobs[2].Id).ToListAsync();
        Assert.Equal(3, actualJobs.Count);
    }

    [Fact]
    public async Task DeleteRecurrentAsync_Deletes()
    {
        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            Cron = "*/5 * * * *",
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            Status = JobStatus.Scheduled,
        };
        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddAsync(job);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.DeleteRecurrentAsync(job.JobName);

        var jobExists = await dbContext.Jobs.AsNoTracking().Where(x => x.Id == job.Id).AnyAsync();
        Assert.False(jobExists);
    }

    [Fact]
    public void DeleteRecurrent_Deletes()
    {
        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            Cron = "*/5 * * * *",
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            Status = JobStatus.Scheduled,
        };
        using var dbContext = DbHelper.CreateContext();
        dbContext.Add(job);
        dbContext.SaveChanges();

        var storage = DbHelper.CreateJobbyStorage();
        storage.DeleteRecurrent(job.JobName);

        var jobExists = dbContext.Jobs.AsNoTracking().Where(x => x.Id == job.Id).Any();
        Assert.False(jobExists);
    }
}
