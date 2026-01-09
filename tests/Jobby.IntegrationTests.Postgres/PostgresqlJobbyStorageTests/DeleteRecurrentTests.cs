using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class DeleteRecurrentTests
{
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