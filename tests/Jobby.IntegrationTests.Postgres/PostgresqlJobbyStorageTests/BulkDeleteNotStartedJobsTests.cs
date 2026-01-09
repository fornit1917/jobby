using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class BulkDeleteNotStartedJobsTests
{
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
}