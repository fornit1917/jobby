using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class BulkDeleteRecurrentTests
{
    [Fact]
    public async Task BulkDeleteJobsAsync_DeletesSpecifiedJobs()
    {
        var jobs = new List<JobDbModel>
        {
            // should be deleted
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                Schedule = "*/3 * * * *",
                JobParam = "param",
                Status = JobStatus.Scheduled,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            },
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                Schedule = "*/3 * * * *",
                JobParam = "param",
                Status = JobStatus.Processing,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            },
            
            // should not be deleted
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
                Schedule = "*/3 * * * *",
                JobParam = "param",
                Status = JobStatus.Scheduled,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            },
        };

        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync(jobs, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateJobbyStorage();
        await storage.BulkDeleteRecurrentAsync([jobs[0].Id, jobs[1].Id, jobs[2].Id]);

        var actualJobs = await dbContext.Jobs.AsNoTracking()
            .Where(x => jobs.Select(j => j.Id).Contains(x.Id))
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        Assert.Equal(2, actualJobs.Count);
        Assert.Contains(actualJobs, x => x.Id == jobs[2].Id);
        Assert.Contains(actualJobs, x => x.Id == jobs[3].Id);
    }
    
    [Fact]
    public void BulkDeleteJobs_DeletesSpecifiedJobs()
    {
        var jobs = new List<JobDbModel>
        {
            // should be deleted
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                Schedule = "*/3 * * * *",
                JobParam = "param",
                Status = JobStatus.Scheduled,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            },
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                Schedule = "*/3 * * * *",
                JobParam = "param",
                Status = JobStatus.Processing,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            },
            
            // should not be deleted
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
                Schedule = "*/3 * * * *",
                JobParam = "param",
                Status = JobStatus.Scheduled,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            },
        };

        using var dbContext = DbHelper.CreateContext();
        dbContext.AddRange(jobs); 
        dbContext.SaveChanges();

        var storage = DbHelper.CreateJobbyStorage();
        storage.BulkDeleteRecurrent([jobs[0].Id, jobs[1].Id, jobs[2].Id]);

        var actualJobs = dbContext.Jobs.AsNoTracking()
            .Where(x => jobs.Select(j => j.Id).Contains(x.Id))
            .ToList();
        
        Assert.Equal(2, actualJobs.Count);
        Assert.Contains(actualJobs, x => x.Id == jobs[2].Id);
        Assert.Contains(actualJobs, x => x.Id == jobs[3].Id);
    }
}