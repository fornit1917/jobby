using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection("Jobby.Postgres.IntegrationTests")]
public class UpdateMethodsTests
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

    [Fact]
    public async Task UpdateProcessingJobToCompletedAsync_NoNextJob_UpdatesCurrent()
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
            Error = "prev error",
            ServerId = Guid.NewGuid().ToString(),
        };
        await dbContext.AddAsync(job);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.UpdateProcessingJobToCompletedAsync(job.ToProcessingJob());

        var actualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.Id);
        Assert.Equal(JobStatus.Completed, actualJob.Status);
        Assert.NotNull(actualJob.LastFinishedAt);
        Assert.Equal(DateTime.UtcNow, actualJob.LastFinishedAt.Value, TimeSpan.FromSeconds(3));
        Assert.Null(actualJob.Error);
    }

    [Fact]
    public async Task UpdateProcessingJobToCompletedAsync_HasNextJob_CompletesCurrentAndSchedulesNext()
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
            ServerId = Guid.NewGuid().ToString(),
            Error = "prev error"
        };
        await dbContext.AddRangeAsync([nextJob, job]);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.UpdateProcessingJobToCompletedAsync(job.ToProcessingJob(), job.NextJobId);

        var actualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.Id);
        Assert.Equal(JobStatus.Completed, actualJob.Status);
        Assert.NotNull(actualJob.LastFinishedAt);
        Assert.Equal(DateTime.UtcNow, actualJob.LastFinishedAt.Value, TimeSpan.FromSeconds(3));
        Assert.Null(actualJob.Error);

        var actualNextJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.NextJobId);
        Assert.Equal(JobStatus.Scheduled, actualNextJob.Status);
    }

    [Fact]
    public async Task UpdateProcessingJobToCompletedAsync_CurrentNotProcessing_DoesNothing()
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
            Status = JobStatus.Failed,
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            ServerId = Guid.NewGuid().ToString(),
            Error = "prev error"
        };
        await dbContext.AddRangeAsync([nextJob, job]);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.UpdateProcessingJobToCompletedAsync(job.ToProcessingJob(), job.NextJobId);

        var actualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.Id);
        Assert.Equal(JobStatus.Failed, actualJob.Status);

        var actualNextJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.NextJobId);
        Assert.Equal(JobStatus.WaitingPrev, actualNextJob.Status);
    }

    [Fact]
    public async Task UpdateProcessingJobToCompletedAsync_CurrentOnOtherServer_DoesNothing()
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
            ServerId = "new_server",
            Error = "prev error"
        };
        await dbContext.AddRangeAsync([nextJob, job]);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.UpdateProcessingJobToCompletedAsync(new ProcessingJob(job.Id, "old_server"), job.NextJobId);

        var actualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.Id);
        Assert.Equal(JobStatus.Processing, actualJob.Status);

        var actualNextJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.NextJobId);
        Assert.Equal(JobStatus.WaitingPrev, actualNextJob.Status);
    }
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
