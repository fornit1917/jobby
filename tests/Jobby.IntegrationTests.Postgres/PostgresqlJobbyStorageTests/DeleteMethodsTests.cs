﻿using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection("Jobby.Postgres.IntegrationTests")]
public class DeleteMethodsTests
{
    [Fact]
    public async Task DeleteAsync_NoNextJob_Deletes()
    {
        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            Status = JobStatus.Processing,
        };
        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddAsync(job);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.DeleteAsync(job.Id);

        var jobExists = await dbContext.Jobs.AsNoTracking().Where(x => x.Id == job.Id).AnyAsync();
        Assert.False(jobExists);
    }

    [Fact]
    public async Task DeleteAsync_HasNextJob_DeletesCurrentAndSchedulesNext()
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
        };
        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync([job, nextJob]);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.DeleteAsync(job.Id, job.NextJobId);

        var jobExists = await dbContext.Jobs.AsNoTracking().Where(x => x.Id == job.Id).AnyAsync();
        Assert.False(jobExists);

        var actualNextJob = await dbContext.Jobs.AsNoTracking().Where(x => x.Id == nextJob.Id).FirstOrDefaultAsync();
        Assert.NotNull(actualNextJob);
        Assert.Equal(JobStatus.Scheduled, actualNextJob.Status);
    }

    [Fact]
    public async Task BulkDeleteAsync_NoNextJobs_Deletes()
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
                Status = JobStatus.Processing,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            },
        };

        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync(jobs);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.BulkDeleteAsync(jobs.Select(x => x.Id).ToList());

        var actualJobs = await dbContext.Jobs.AsNoTracking()
            .Where(x => x.Id == jobs[0].Id || x.Id == jobs[1].Id).ToListAsync();
        Assert.Empty(actualJobs);
    }

    [Fact]
    public async Task BulkDeleteAsync_HaveNextJobs_DeletesCurrentAndSchedulesNext()
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
            },
        };

        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync(jobs.Concat(nextJobs));
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.BulkDeleteAsync(jobs.Select(x => x.Id).ToList(), nextJobs.Select(x => x.Id).ToList());

        var actualJobs = await dbContext.Jobs.AsNoTracking()
            .Where(x => x.Id == jobs[0].Id || x.Id == jobs[1].Id).ToListAsync();
        Assert.Empty(actualJobs);

        var firstActualNextJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == nextJobs[0].Id);
        Assert.Equal(JobStatus.Scheduled, firstActualNextJob.Status);
        var secondActualNextJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == nextJobs[1].Id);
        Assert.Equal(JobStatus.Scheduled, secondActualNextJob.Status);
    }

    [Fact]
    public void BulkDelete_Deletes()
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
                Status = JobStatus.Processing,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            },
        };

        using var dbContext = DbHelper.CreateContext();
        dbContext.AddRange(jobs);
        dbContext.SaveChanges();

        var storage = DbHelper.CreateJobbyStorage();
        storage.BulkDelete(jobs.Select(x => x.Id).ToList());

        var actualJobs = dbContext.Jobs.AsNoTracking()
            .Where(x => x.Id == jobs[0].Id || x.Id == jobs[1].Id).ToList();
        Assert.Empty(actualJobs);
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
