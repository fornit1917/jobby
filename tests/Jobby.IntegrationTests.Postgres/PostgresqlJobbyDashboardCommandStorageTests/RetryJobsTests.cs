using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyDashboardCommandStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class RetryJobsTests
{
    [Theory]
    [InlineData(JobStatus.Failed)]
    [InlineData(JobStatus.Frozen)]
    public async Task RetryJobsAsync_FailedOrFrozenJob_ResetsToFreshScheduledAndReturns1(JobStatus status)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var id = Guid.NewGuid();
        var job = TriggerJobsTests.NewJob(id, status, scheduledStartAt: DateTime.UtcNow.AddMinutes(-5));
        job.StartedCount = 3;
        job.Error = "boom";
        job.ServerId = "server-1";
        job.LastStartedAt = DateTime.UtcNow.AddMinutes(-4);
        job.LastFinishedAt = DateTime.UtcNow.AddMinutes(-3);
        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var affected = await DbHelper.CreateDashboardCommandStorage().RetryJobsAsync([id], now, cancellationToken);

        Assert.Equal(1, affected);
        var reread = await DbHelper.CreateDashboardReadStorage().GetJobByIdAsync(id, cancellationToken);
        Assert.Equal(JobStatus.Scheduled, reread!.Status);
        Assert.Equal(0, reread.StartedCount);
        Assert.Null(reread.Error);
        Assert.Null(reread.ServerId);
        Assert.Null(reread.LastStartedAt);
        Assert.Null(reread.LastFinishedAt);
        Assert.True((reread.ScheduledStartAt - now).Duration() < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RetryJobsAsync_CompletedJob_LeavesItUntouchedAndReturns0()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var id = Guid.NewGuid();
        db.Jobs.Add(TriggerJobsTests.NewJob(id, JobStatus.Completed, scheduledStartAt: DateTime.UtcNow.AddMinutes(-5)));
        await db.SaveChangesAsync(cancellationToken);

        var affected = await DbHelper.CreateDashboardCommandStorage().RetryJobsAsync([id], DateTime.UtcNow, cancellationToken);

        Assert.Equal(0, affected);
        var reread = await DbHelper.CreateDashboardReadStorage().GetJobByIdAsync(id, cancellationToken);
        Assert.Equal(JobStatus.Completed, reread!.Status);
    }

    [Fact]
    public async Task RetryJobsAsync_FailedGroupLocker_LeavesItUntouchedAndReturns0()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var id = Guid.NewGuid();
        var job = TriggerJobsTests.NewJob(id, JobStatus.Failed, scheduledStartAt: DateTime.UtcNow);
        job.SerializableGroupId = "group-1";
        job.LockGroupIfFailed = true;
        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        var affected = await DbHelper.CreateDashboardCommandStorage().RetryJobsAsync([id], DateTime.UtcNow, cancellationToken);

        Assert.Equal(0, affected);
        var reread = await DbHelper.CreateDashboardReadStorage().GetJobByIdAsync(id, cancellationToken);
        Assert.Equal(JobStatus.Failed, reread!.Status);
    }

    [Fact]
    public async Task RetryJobsAsync_FrozenSiblingInLockedGroup_IsRequeuedAndReturns1()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var lockerId = Guid.NewGuid();
        var siblingId = Guid.NewGuid();
        var locker = TriggerJobsTests.NewJob(lockerId, JobStatus.Failed, scheduledStartAt: DateTime.UtcNow);
        locker.SerializableGroupId = "group-1";
        locker.LockGroupIfFailed = true;
        var sibling = TriggerJobsTests.NewJob(siblingId, JobStatus.Frozen, scheduledStartAt: DateTime.UtcNow);
        sibling.SerializableGroupId = "group-1";
        db.Jobs.AddRange(locker, sibling);
        await db.SaveChangesAsync(cancellationToken);

        var affected = await DbHelper.CreateDashboardCommandStorage().RetryJobsAsync([siblingId], DateTime.UtcNow, cancellationToken);

        Assert.Equal(1, affected);
        var reread = await DbHelper.CreateDashboardReadStorage().GetJobByIdAsync(siblingId, cancellationToken);
        Assert.Equal(JobStatus.Scheduled, reread!.Status);
    }
}