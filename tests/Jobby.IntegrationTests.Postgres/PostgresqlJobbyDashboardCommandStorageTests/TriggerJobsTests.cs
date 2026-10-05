using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyDashboardCommandStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class TriggerJobsTests
{
    [Fact]
    public async Task TriggerJobsAsync_ScheduledJob_MovesStartToNowAndReturns1()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var id = Guid.NewGuid();
        db.Jobs.Add(NewJob(id, JobStatus.Scheduled, scheduledStartAt: DateTime.UtcNow.AddDays(1)));
        await db.SaveChangesAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var affected = await DbHelper.CreateDashboardCommandStorage().TriggerJobsAsync([id], now, cancellationToken);

        Assert.Equal(1, affected);
        var job = await DbHelper.CreateDashboardReadStorage().GetJobByIdAsync(id, cancellationToken);
        Assert.Equal(JobStatus.Scheduled, job!.Status);
        Assert.True((job.ScheduledStartAt - now).Duration() < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task TriggerJobsAsync_NonScheduledJob_LeavesItUntouchedAndReturns0()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var id = Guid.NewGuid();
        var originalStart = DateTime.UtcNow.AddDays(1);
        db.Jobs.Add(NewJob(id, JobStatus.Processing, scheduledStartAt: originalStart));
        await db.SaveChangesAsync(cancellationToken);

        var affected = await DbHelper.CreateDashboardCommandStorage().TriggerJobsAsync([id], DateTime.UtcNow, cancellationToken);

        Assert.Equal(0, affected);
        var job = await DbHelper.CreateDashboardReadStorage().GetJobByIdAsync(id, cancellationToken);
        Assert.Equal(JobStatus.Processing, job!.Status);
        Assert.True((job.ScheduledStartAt - originalStart).Duration() < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task TriggerJobsAsync_RecurrentDefinition_LeavesItUntouchedAndReturns0()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var id = Guid.NewGuid();
        var job = NewJob(id, JobStatus.Scheduled, scheduledStartAt: DateTime.UtcNow.AddDays(1));
        job.Schedule = "0 0 * * *";
        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        var affected = await DbHelper.CreateDashboardCommandStorage().TriggerJobsAsync([id], DateTime.UtcNow, cancellationToken);

        Assert.Equal(0, affected);
    }

    internal static JobDbModel NewJob(Guid id, JobStatus status, DateTime scheduledStartAt) => new()
    {
        Id = id,
        JobName = "my-job",
        JobParam = "{}",
        Status = status,
        QueueName = "default",
        CreatedAt = DateTime.UtcNow.AddMinutes(-10),
        ScheduledStartAt = scheduledStartAt,
        StartedCount = 0,
        CanBeRestarted = true,
    };
}