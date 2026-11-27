using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyDashboardCommandStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class TriggerJobsTests
{
    private static readonly DateTime TriggerTime = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task TriggerJobsAsync_FutureScheduledJob_MovesSameRowToNowAndRepeatedRequestReturns1(
        bool recurrent, bool exclusive)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var id = Guid.NewGuid();
        var original = NewJob(id, JobStatus.Scheduled, scheduledStartAt: TriggerTime.AddDays(1));
        original.Schedule = recurrent ? "0 0 * * *" : null;
        original.SchedulerType = recurrent ? "cron" : null;
        original.IsExclusive = exclusive;
        db.Jobs.Add(original);
        await db.SaveChangesAsync(cancellationToken);

        var storage = DbHelper.CreateDashboardCommandStorage();
        var affected = await storage.TriggerJobsAsync([id], TriggerTime, cancellationToken);

        Assert.Equal(1, affected);
        var triggered = Assert.Single(await db.Jobs.AsNoTracking().ToListAsync(cancellationToken));
        Assert.Equal(id, triggered.Id);
        Assert.Equal(JobStatus.Scheduled, triggered.Status);
        Assert.Equal(TriggerTime, triggered.ScheduledStartAt);
        Assert.Equal(original.Schedule, triggered.Schedule);
        Assert.Equal(original.SchedulerType, triggered.SchedulerType);
        Assert.Equal(exclusive, triggered.IsExclusive);

        var repeated = await storage.TriggerJobsAsync([id], TriggerTime.AddMinutes(1), cancellationToken);

        Assert.Equal(1, repeated);
        var unchanged = Assert.Single(await db.Jobs.AsNoTracking().ToListAsync(cancellationToken));
        Assert.Equal(id, unchanged.Id);
        Assert.Equal(JobStatus.Scheduled, unchanged.Status);
        Assert.Equal(TriggerTime, unchanged.ScheduledStartAt);
        Assert.Equal(original.Schedule, unchanged.Schedule);
        Assert.Equal(original.SchedulerType, unchanged.SchedulerType);
        Assert.Equal(exclusive, unchanged.IsExclusive);
    }

    [Theory]
    [InlineData(false, -1)]
    [InlineData(false, 0)]
    [InlineData(true, -1)]
    [InlineData(true, 0)]
    public async Task TriggerJobsAsync_AlreadyDueScheduledJob_PreservesStartAndReturns1(bool recurrent, int daysFromNow)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var id = Guid.NewGuid();
        var originalStart = TriggerTime.AddDays(daysFromNow);
        var original = NewJob(id, JobStatus.Scheduled, scheduledStartAt: originalStart);
        original.Schedule = recurrent ? "0 0 * * *" : null;
        db.Jobs.Add(original);
        await db.SaveChangesAsync(cancellationToken);

        var affected = await DbHelper.CreateDashboardCommandStorage().TriggerJobsAsync([id], TriggerTime, cancellationToken);

        Assert.Equal(1, affected);
        var job = await DbHelper.CreateDashboardReadStorage().GetJobByIdAsync(id, cancellationToken);
        Assert.Equal(JobStatus.Scheduled, job!.Status);
        Assert.Equal(originalStart, job.ScheduledStartAt);
        Assert.Equal(original.Schedule, job.Schedule);
    }

    [Theory]
    [InlineData(JobStatus.Processing, false)]
    [InlineData(JobStatus.Processing, true)]
    [InlineData(JobStatus.Frozen, false)]
    [InlineData(JobStatus.Frozen, true)]
    [InlineData(JobStatus.Failed, false)]
    [InlineData(JobStatus.Failed, true)]
    [InlineData(JobStatus.Completed, false)]
    [InlineData(JobStatus.Completed, true)]
    [InlineData(JobStatus.WaitingPrev, false)]
    [InlineData(JobStatus.WaitingPrev, true)]
    public async Task TriggerJobsAsync_NonScheduledJob_LeavesItUntouchedAndReturns0(JobStatus status, bool recurrent)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var id = Guid.NewGuid();
        var originalStart = TriggerTime.AddDays(1);
        var original = NewJob(id, status, scheduledStartAt: originalStart);
        original.Schedule = recurrent ? "0 0 * * *" : null;
        db.Jobs.Add(original);
        await db.SaveChangesAsync(cancellationToken);

        var affected = await DbHelper.CreateDashboardCommandStorage().TriggerJobsAsync([id], TriggerTime, cancellationToken);

        Assert.Equal(0, affected);
        var job = await DbHelper.CreateDashboardReadStorage().GetJobByIdAsync(id, cancellationToken);
        Assert.Equal(status, job!.Status);
        Assert.Equal(originalStart, job.ScheduledStartAt);
        Assert.Equal(original.Schedule, job.Schedule);
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
