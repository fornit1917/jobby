using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyDashboardReadStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class GetStatsTests
{
    [Fact]
    public async Task GetStatsAsync_ReturnsCountsByStatus()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.AddRange(
            NewJob(JobStatus.Scheduled), NewJob(JobStatus.Scheduled),
            NewJob(JobStatus.Failed));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var stats = await storage.GetStatsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, stats.StatusCounts.Single(c => c.Status == JobStatus.Scheduled).Count);
        Assert.Equal(1, stats.StatusCounts.Single(c => c.Status == JobStatus.Failed).Count);
        Assert.Equal(3, stats.Total);
    }

    [Fact]
    public async Task GetStatsAsync_ExcludesRecurrentTemplates()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.AddRange(
            NewJob(JobStatus.Scheduled),
            Recurrent(JobStatus.Scheduled));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var stats = await storage.GetStatsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, stats.StatusCounts.Single(c => c.Status == JobStatus.Scheduled).Count);
        Assert.Equal(1, stats.Total);
    }

    private static JobDbModel NewJob(JobStatus status) => new()
    {
        Id = Guid.NewGuid(),
        JobName = Guid.NewGuid().ToString(),
        Status = status,
        CreatedAt = DateTime.UtcNow,
        ScheduledStartAt = DateTime.UtcNow,
    };

    private static JobDbModel Recurrent(JobStatus status)
    {
        var job = NewJob(status);
        job.Schedule = "*/5 * * * * *";
        return job;
    }
}