using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class DeleteExclusiveByNameTests
{
    [Fact]
    public async Task DeleteExclusiveByNameAsync_Deletes()
    {
        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            Schedule = "*/5 * * * *",
            IsExclusive = true,
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            Status = JobStatus.Scheduled,
        };
        await using var dbContext = DbHelper.CreateContext();
        await dbContext.AddAsync(job, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateJobbyStorage();
        await storage.DeleteExclusiveByNameAsync(job.JobName);

        var jobExists = await dbContext.Jobs.AsNoTracking()
            .Where(x => x.Id == job.Id)
            .AnyAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(jobExists);
    }

    [Fact]
    public void DeleteExclusiveByName_Deletes()
    {
        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            Schedule = "*/5 * * * *",
            IsExclusive = true,
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            Status = JobStatus.Scheduled,
        };
        using var dbContext = DbHelper.CreateContext();
        dbContext.Add(job);
        dbContext.SaveChanges();

        var storage = DbHelper.CreateJobbyStorage();
        storage.DeleteExclusiveByName(job.JobName);

        var jobExists = dbContext.Jobs.AsNoTracking().Any(x => x.Id == job.Id);
        Assert.False(jobExists);
    }    
}