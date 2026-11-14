using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.IntegrationTests.Postgres.Helpers;
using Jobby.TestsUtils;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyDashboardReadStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class SharedCountsTests
{
    private static readonly TimeSpan Duration = TimeSpan.FromMinutes(1);

    [Fact]
    public async Task StatsQueuesAndListTotals_ReuseOneSnapshotUntilItExpires()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.AddRange(NewJob(JobStatus.Scheduled, "job-1"), NewJob(JobStatus.Scheduled, "job-2"));
        await db.SaveChangesAsync(cancellationToken);
        var time = new ManualTimeProvider();
        var storage = DbHelper.CreateDashboardStorages(Duration, time).Read;
        var firstPageOfOne = new DashboardJobsQuery { Status = JobStatus.Scheduled, PageSize = 1 };

        Assert.Equal(2, (await storage.GetStatsAsync(cancellationToken)).Total);
        db.Jobs.Add(NewJob(JobStatus.Scheduled, "job-3"));
        await db.SaveChangesAsync(cancellationToken);

        Assert.Equal(2, (await storage.GetStatsAsync(cancellationToken)).Total);
        Assert.Equal(2, (await storage.GetQueuesAsync(cancellationToken)).Single().StatusCounts.Single().Count);
        Assert.Equal(2, (await storage.GetJobsAsync(firstPageOfOne, cancellationToken)).TotalCount);
        // A search cannot be answered from the per-queue counts, so its total is always counted.
        Assert.Equal(3, (await storage.GetJobsAsync(
            firstPageOfOne with { JobNameSearch = "job" }, cancellationToken)).TotalCount);

        time.Advance(Duration);

        Assert.Equal(3, (await storage.GetStatsAsync(cancellationToken)).Total);
        Assert.Equal(3, (await storage.GetJobsAsync(firstPageOfOne, cancellationToken)).TotalCount);
    }

    [Fact]
    public async Task GetJobsAsync_WithoutLoadedCounts_CountsLiveAndDoesNotLoadThem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.AddRange(NewJob(JobStatus.Scheduled, "job-1"), NewJob(JobStatus.Scheduled, "job-2"));
        await db.SaveChangesAsync(cancellationToken);
        var storage = DbHelper.CreateDashboardStorages(Duration, new ManualTimeProvider()).Read;
        var firstPageOfOne = new DashboardJobsQuery { PageSize = 1 };
        Assert.Equal(2, (await storage.GetJobsAsync(firstPageOfOne, cancellationToken)).TotalCount);

        db.Jobs.Add(NewJob(JobStatus.Scheduled, "job-3"));
        await db.SaveChangesAsync(cancellationToken);

        // Had the first call loaded the shared counts, they would still report 2 here.
        Assert.Equal(3, (await storage.GetJobsAsync(firstPageOfOne, cancellationToken)).TotalCount);
    }

    [Fact]
    public async Task GetJobsAsync_TotalFromOlderCounts_IsNotBelowTheRowsReturned()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.Add(NewJob(JobStatus.Scheduled, "job-1"));
        await db.SaveChangesAsync(cancellationToken);
        var storage = DbHelper.CreateDashboardStorages(Duration, new ManualTimeProvider()).Read;
        Assert.Equal(1, (await storage.GetStatsAsync(cancellationToken)).Total);

        db.Jobs.AddRange(NewJob(JobStatus.Scheduled, "job-2"), NewJob(JobStatus.Scheduled, "job-3"));
        await db.SaveChangesAsync(cancellationToken);
        var page = await storage.GetJobsAsync(new DashboardJobsQuery(), cancellationToken);

        Assert.Equal(3, page.Items.Count);
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task DeleteJobsAsync_DropsCounts_SoTheDeletedJobIsNotCounted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var deleted = NewJob(JobStatus.Scheduled, "deleted");
        db.Jobs.AddRange(deleted, NewJob(JobStatus.Scheduled, "kept"));
        await db.SaveChangesAsync(cancellationToken);
        var (read, command) = DbHelper.CreateDashboardStorages(Duration, new ManualTimeProvider());
        Assert.Equal(2, (await read.GetStatsAsync(cancellationToken)).Total);

        Assert.Equal(1, await command.DeleteJobsAsync([deleted.Id], cancellationToken));

        Assert.Equal(1, (await read.GetStatsAsync(cancellationToken)).Total);
        Assert.Equal(1, (await read.GetJobsAsync(
            new DashboardJobsQuery { PageSize = 1, Page = 2 }, cancellationToken)).TotalCount);
    }

    [Fact]
    public async Task RetryJobsAsync_DropsCounts_SoTheJobIsCountedInItsNewStatus()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var failed = NewJob(JobStatus.Failed, "failed");
        db.Jobs.Add(failed);
        await db.SaveChangesAsync(cancellationToken);
        var (read, command) = DbHelper.CreateDashboardStorages(Duration, new ManualTimeProvider());
        Assert.Equal(JobStatus.Failed, (await read.GetStatsAsync(cancellationToken)).StatusCounts.Single().Status);

        Assert.Equal(1, await command.RetryJobsAsync([failed.Id], DateTime.UtcNow, cancellationToken));

        Assert.Equal(new DashboardStatusCount(JobStatus.Scheduled, 1),
            (await read.GetStatsAsync(cancellationToken)).StatusCounts.Single());
    }

    [Fact]
    public async Task SeparateInstances_KeepTheirOwnCounts_WhileRowsAreAlwaysRead()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var start = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var deleted = NewJob(JobStatus.Failed, "deleted");
        deleted.ScheduledStartAt = start;
        var kept = NewJob(JobStatus.Failed, "kept");
        kept.ScheduledStartAt = start.AddMinutes(1);
        db.Jobs.AddRange(deleted, kept);
        await db.SaveChangesAsync(cancellationToken);
        // Two application instances (for example two pods) against one database.
        var firstInstance = DbHelper.CreateDashboardStorages(Duration, new ManualTimeProvider());
        var secondTime = new ManualTimeProvider();
        var secondInstance = DbHelper.CreateDashboardStorages(Duration, secondTime);
        Assert.Equal(2, (await firstInstance.Read.GetStatsAsync(cancellationToken)).Total);
        Assert.Equal(2, (await secondInstance.Read.GetStatsAsync(cancellationToken)).Total);

        Assert.Equal(1, await firstInstance.Command.DeleteJobsAsync([deleted.Id], cancellationToken));

        // The instance that executed the action shows the new counts at once.
        Assert.Equal(1, (await firstInstance.Read.GetStatsAsync(cancellationToken)).Total);
        // Any instance lists only the jobs that exist: rows are never cached.
        var listedBySecond = await secondInstance.Read.GetJobsAsync(new DashboardJobsQuery(), cancellationToken);
        Assert.Equal(new[] { "kept" }, listedBySecond.Items.Select(item => item.JobName));
        // Pins the accepted trade-off: another instance keeps its own counts until they expire, so its total
        // can include the deleted job for up to the cache duration. Sharing counts between instances would
        // change these two asserts to 1.
        Assert.Equal(2, listedBySecond.TotalCount);
        Assert.Equal(2, (await secondInstance.Read.GetStatsAsync(cancellationToken)).Total);

        secondTime.Advance(Duration);

        Assert.Equal(1, (await secondInstance.Read.GetStatsAsync(cancellationToken)).Total);
        Assert.Equal(1, (await secondInstance.Read.GetJobsAsync(new DashboardJobsQuery(), cancellationToken)).TotalCount);
    }

    private static JobDbModel NewJob(JobStatus status, string name) => new()
    {
        Id = Guid.NewGuid(),
        JobName = name,
        Status = status,
        QueueName = "default",
        CreatedAt = DateTime.UtcNow,
        ScheduledStartAt = DateTime.UtcNow,
    };
}
