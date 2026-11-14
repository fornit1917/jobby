using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyDashboardCommandStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class DeleteRecurrentJobsTests
{
    [Fact]
    public async Task DeleteRecurrentJobsAsync_RecurrentJob_RemovesAndReturns1()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var id = Guid.NewGuid();
        var job = TriggerJobsTests.NewJob(id, JobStatus.Scheduled, scheduledStartAt: DateTime.UtcNow);
        job.Schedule = "0 0 * * *";
        job.SchedulerType = "cron";
        job.IsExclusive = true;
        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        var affected = await DbHelper.CreateDashboardCommandStorage().DeleteRecurrentJobsAsync([id], cancellationToken);

        Assert.Equal(1, affected);
        Assert.Null(await DbHelper.CreateDashboardReadStorage().GetJobByIdAsync(id, cancellationToken));
    }

    [Fact]
    public async Task DeleteRecurrentJobsAsync_NonRecurrentJob_IsNotDeletedAndReturns0()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var id = Guid.NewGuid();
        db.Jobs.Add(TriggerJobsTests.NewJob(id, JobStatus.Scheduled, scheduledStartAt: DateTime.UtcNow));
        await db.SaveChangesAsync(cancellationToken);

        var affected = await DbHelper.CreateDashboardCommandStorage().DeleteRecurrentJobsAsync([id], cancellationToken);

        Assert.Equal(0, affected);
        Assert.NotNull(await DbHelper.CreateDashboardReadStorage().GetJobByIdAsync(id, cancellationToken));
    }

    [Fact]
    public async Task DeleteRecurrentJobsAsync_RecurrentGroupLocker_IsNotDeletedAndReturns0()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var id = Guid.NewGuid();
        var job = TriggerJobsTests.NewJob(id, JobStatus.Processing, scheduledStartAt: DateTime.UtcNow);
        job.Schedule = "0 0 * * *";
        job.SerializableGroupId = "group-1";
        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        var affected = await DbHelper.CreateDashboardCommandStorage().DeleteRecurrentJobsAsync([id], cancellationToken);

        Assert.Equal(0, affected);
        Assert.NotNull(await DbHelper.CreateDashboardReadStorage().GetJobByIdAsync(id, cancellationToken));
    }
}