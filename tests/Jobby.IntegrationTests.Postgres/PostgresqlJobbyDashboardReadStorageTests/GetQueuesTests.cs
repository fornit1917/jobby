using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyDashboardReadStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class GetQueuesTests
{
    [Fact]
    public async Task GetQueuesAsync_GroupsCountsPerQueueAndStatus()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.AddRange(
            NewJob("default", JobStatus.Scheduled),
            NewJob("default", JobStatus.Failed),
            NewJob("emails", JobStatus.Scheduled));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var queues = await storage.GetQueuesAsync(TestContext.Current.CancellationToken);

        var def = queues.Single(q => q.QueueName == "default");
        Assert.Equal(1, def.StatusCounts.Single(c => c.Status == JobStatus.Scheduled).Count);
        Assert.Equal(1, def.StatusCounts.Single(c => c.Status == JobStatus.Failed).Count);
        var emails = queues.Single(q => q.QueueName == "emails");
        Assert.Equal(1, emails.StatusCounts.Single(c => c.Status == JobStatus.Scheduled).Count);
    }

    [Fact]
    public async Task GetQueuesAsync_ExcludesRecurrentTemplates()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.AddRange(
            NewJob("default", JobStatus.Scheduled),
            Recurrent("recurrent", JobStatus.Scheduled));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var queues = await storage.GetQueuesAsync(TestContext.Current.CancellationToken);

        var def = queues.Single(q => q.QueueName == "default");
        Assert.Equal(1, def.StatusCounts.Single(c => c.Status == JobStatus.Scheduled).Count);
        Assert.DoesNotContain(queues, q => q.QueueName == "recurrent");
    }

    private static JobDbModel NewJob(string queue, JobStatus status) => new()
    {
        Id = Guid.NewGuid(),
        JobName = Guid.NewGuid().ToString(),
        Status = status,
        QueueName = queue,
        CreatedAt = DateTime.UtcNow,
        ScheduledStartAt = DateTime.UtcNow,
    };

    private static JobDbModel Recurrent(string queue, JobStatus status)
    {
        var job = NewJob(queue, status);
        job.Schedule = "*/5 * * * * *";
        return job;
    }
}