using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Npgsql;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyDashboardReadStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class GetJobByIdTests
{
    [Fact]
    public async Task GetJobByIdAsync_ExistingJob_ReturnsAllFields()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var id = Guid.NewGuid();
        var nextId = Guid.NewGuid();
        var created = DateTime.UtcNow.AddMinutes(-5);
        var started = DateTime.UtcNow.AddMinutes(-3);
        var finished = DateTime.UtcNow.AddMinutes(-1);
        var scheduled = DateTime.UtcNow;
        var job = new JobDbModel
        {
            Id = id,
            JobName = "my-job",
            JobParam = "{\"x\":1}",
            Status = JobStatus.Failed,
            Error = "boom",
            QueueName = "emails",
            CreatedAt = created,
            ScheduledStartAt = scheduled,
            LastStartedAt = started,
            LastFinishedAt = finished,
            StartedCount = 2,
            ServerId = "server-1",
            NextJobId = nextId,
            Schedule = "0 0 * * *",
            SchedulerType = "cron",
            SerializableGroupId = "group-1",
            LockGroupIfFailed = true,
            IsExclusive = true,
            CanBeRestarted = true,
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var d = await storage.GetJobByIdAsync(id, TestContext.Current.CancellationToken);

        Assert.NotNull(d);
        Assert.Equal(id, d.Id);
        Assert.Equal("my-job", d.JobName);
        Assert.Equal(JobStatus.Failed, d.Status);
        Assert.Equal("emails", d.QueueName);
        Assert.Equal("{\"x\":1}", d.JobParam);
        Assert.Equal("boom", d.Error);
        Assert.Equal(2, d.StartedCount);
        Assert.Equal("server-1", d.ServerId);
        Assert.Equal(nextId, d.NextJobId);
        Assert.Equal("0 0 * * *", d.Schedule);
        Assert.Equal("cron", d.SchedulerType);
        Assert.Equal("group-1", d.SerializableGroupId);
        Assert.True(d.LockGroupIfFailed);
        Assert.True(d.IsExclusive);
        Assert.True(d.CanBeRestarted);
        Assert.NotNull(d.LastStartedAt);
        Assert.NotNull(d.LastFinishedAt);
    }

    [Fact]
    public async Task GetJobByIdAsync_MissingJob_ReturnsNull()
    {
        await using var _ = await DbHelper.CreateContextAndClearDbAsync();
        var storage = DbHelper.CreateDashboardReadStorage();
        var details = await storage.GetJobByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.Null(details);
    }

    [Fact]
    public async Task GetJobByIdAsync_NullLockGroupIfFailed_ReadsAsFalse()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var id = Guid.NewGuid();

        await using (var conn = await DbHelper.DataSource.OpenConnectionAsync(cancellationToken))
        await using (var cmd = new NpgsqlCommand(
                         @"INSERT INTO jobby_jobs
                  (id, job_name, status, created_at, scheduled_start_at, started_count, queue_name, can_be_restarted, is_exclusive, lock_group_if_failed)
              VALUES ($1, $2, $3, $4, $5, 0, 'default', false, false, NULL)", conn))
        {
            cmd.Parameters.Add(new NpgsqlParameter { Value = id });
            cmd.Parameters.Add(new NpgsqlParameter { Value = "nullable-bool-job" });
            cmd.Parameters.Add(new NpgsqlParameter { Value = (int)JobStatus.Scheduled });
            cmd.Parameters.Add(new NpgsqlParameter { Value = DateTime.UtcNow });
            cmd.Parameters.Add(new NpgsqlParameter { Value = DateTime.UtcNow });
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        var storage = DbHelper.CreateDashboardReadStorage();
        var d = await storage.GetJobByIdAsync(id, cancellationToken);

        Assert.NotNull(d);
        Assert.False(d.LockGroupIfFailed);
    }

    [Fact]
    public async Task GetJobByIdAsync_SetsIsGroupLocker_ForFailedGroupLocker()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var lockerId = Guid.NewGuid();
        db.Jobs.Add(new JobDbModel
        {
            Id = lockerId, JobName = "locker", Status = JobStatus.Failed, QueueName = "default",
            CreatedAt = DateTime.UtcNow, ScheduledStartAt = DateTime.UtcNow,
            SerializableGroupId = "g1", LockGroupIfFailed = true,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var details = await DbHelper.CreateDashboardReadStorage()
            .GetJobByIdAsync(lockerId, TestContext.Current.CancellationToken);

        Assert.NotNull(details);
        Assert.True(details.IsGroupLocker);
    }
}