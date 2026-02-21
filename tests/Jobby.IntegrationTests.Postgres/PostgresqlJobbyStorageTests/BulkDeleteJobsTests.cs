using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class BulkDeleteJobsTests
{
    [Fact]
    public async Task BulkDeleteJobsAsync_DeletesSpecifiedJobs()
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
                Status = JobStatus.Processing,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            },
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                JobParam = "param",
                Status = JobStatus.Scheduled,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            },
        };

        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddRangeAsync(jobs);
        await dbContext.SaveChangesAsync();

        var storage = DbHelper.CreateJobbyStorage();
        await storage.BulkDeleteJobsAsync([jobs[0].Id, jobs[1].Id]);

        var actualDeletedJobs = await dbContext.Jobs.AsNoTracking()
            .Where(x => x.Id == jobs[0].Id || x.Id == jobs[1].Id).ToListAsync();
        Assert.Empty(actualDeletedJobs);

        var actualNotDeletedJob = await dbContext.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobs[2].Id);
        Assert.NotNull(actualNotDeletedJob);
    }
    
    [Fact]
    public void BulkDeleteJobs_DeletesSpecifiedJobs()
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
                Status = JobStatus.Processing,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            },
            new JobDbModel
            {
                Id = Guid.NewGuid(),
                JobName = Guid.NewGuid().ToString(),
                JobParam = "param",
                Status = JobStatus.Scheduled,
                ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            },
        };

        using var dbContext = DbHelper.CreateContext();
        dbContext.AddRange(jobs); 
        dbContext.SaveChanges();

        var storage = DbHelper.CreateJobbyStorage();
        storage.BulkDeleteJobs([jobs[0].Id, jobs[1].Id]);

        var actualDeletedJobs = dbContext.Jobs.AsNoTracking()
            .Where(x => x.Id == jobs[0].Id || x.Id == jobs[1].Id).ToList();
        Assert.Empty(actualDeletedJobs);

        var actualNotDeletedJob = dbContext.Jobs.AsNoTracking()
            .FirstOrDefault(x => x.Id == jobs[2].Id);
        Assert.NotNull(actualNotDeletedJob);
    }
}