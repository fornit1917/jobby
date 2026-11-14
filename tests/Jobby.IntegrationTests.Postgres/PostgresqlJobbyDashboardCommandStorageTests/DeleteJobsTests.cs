using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyDashboardCommandStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class DeleteJobsTests
{
    [Theory]
    [InlineData(JobStatus.Scheduled)]
    [InlineData(JobStatus.WaitingPrev)]
    [InlineData(JobStatus.Failed)]
    [InlineData(JobStatus.Frozen)]
    [InlineData(JobStatus.Completed)]
    public async Task DeleteJobsAsync_AnyNonProcessingStatus_RemovesAndReturns1(JobStatus status)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var id = Guid.NewGuid();
        db.Jobs.Add(TriggerJobsTests.NewJob(id, status, scheduledStartAt: DateTime.UtcNow));
        await db.SaveChangesAsync(cancellationToken);

        var affected = await DbHelper.CreateDashboardCommandStorage().DeleteJobsAsync([id], cancellationToken);

        Assert.Equal(1, affected);
        Assert.Null(await DbHelper.CreateDashboardReadStorage().GetJobByIdAsync(id, cancellationToken));
    }

    [Fact]
    public async Task DeleteJobsAsync_ProcessingJob_IsNotDeletedAndReturns0()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var id = Guid.NewGuid();
        db.Jobs.Add(TriggerJobsTests.NewJob(id, JobStatus.Processing, scheduledStartAt: DateTime.UtcNow));
        await db.SaveChangesAsync(cancellationToken);

        var affected = await DbHelper.CreateDashboardCommandStorage().DeleteJobsAsync([id], cancellationToken);

        Assert.Equal(0, affected);
        Assert.NotNull(await DbHelper.CreateDashboardReadStorage().GetJobByIdAsync(id, cancellationToken));
    }

    [Fact]
    public async Task DeleteJobsAsync_RecurrentDefinition_IsNotDeletedAndReturns0()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var id = Guid.NewGuid();
        var job = TriggerJobsTests.NewJob(id, JobStatus.Scheduled, scheduledStartAt: DateTime.UtcNow);
        job.Schedule = "0 0 * * *";
        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        var affected = await DbHelper.CreateDashboardCommandStorage().DeleteJobsAsync([id], cancellationToken);

        Assert.Equal(0, affected);
        Assert.NotNull(await DbHelper.CreateDashboardReadStorage().GetJobByIdAsync(id, cancellationToken));
    }

    [Fact]
    public async Task DeleteJobsAsync_FailedGroupLocker_IsNotDeletedAndReturns0()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var id = Guid.NewGuid();
        var job = TriggerJobsTests.NewJob(id, JobStatus.Failed, scheduledStartAt: DateTime.UtcNow);
        job.SerializableGroupId = "group-1";
        job.LockGroupIfFailed = true;
        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        var affected = await DbHelper.CreateDashboardCommandStorage().DeleteJobsAsync([id], cancellationToken);

        Assert.Equal(0, affected);
        Assert.NotNull(await DbHelper.CreateDashboardReadStorage().GetJobByIdAsync(id, cancellationToken));
    }
}