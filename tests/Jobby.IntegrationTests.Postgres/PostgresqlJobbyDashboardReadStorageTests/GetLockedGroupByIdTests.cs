using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.IntegrationTests.Postgres.Helpers;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyDashboardReadStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class GetLockedGroupByIdTests
{
    private static JobDbModel Job(JobStatus status, string group, DateTime scheduled,
        bool lockIfFailed = false) => new()
    {
        Id = Guid.NewGuid(), JobName = "job", Status = status, QueueName = "default",
        CreatedAt = DateTime.UtcNow.AddMinutes(-30), ScheduledStartAt = scheduled,
        SerializableGroupId = group, LockGroupIfFailed = lockIfFailed,
        StartedCount = status == JobStatus.Failed ? 1 : 0,
    };

    [Fact]
    public async Task ReturnsNull_WhenGroupHasNoStuckLocker()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var details = await DbHelper.CreateDashboardReadStorage()
            .GetLockedGroupByIdAsync("missing", TestContext.Current.CancellationToken);

        Assert.Null(details);
    }

    [Fact]
    public async Task ReturnsJobs_RoleLabeled_OrderedByScheduledStart_ExcludingCompleted()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var now = DateTime.UtcNow;
        db.Jobs.Add(Job(JobStatus.Failed, "g1", now.AddMinutes(0), lockIfFailed: true));
        db.Jobs.Add(Job(JobStatus.Frozen, "g1", now.AddMinutes(1)));
        db.Jobs.Add(Job(JobStatus.WaitingPrev, "g1", now.AddMinutes(2)));
        db.Jobs.Add(Job(JobStatus.Scheduled, "g1", now.AddMinutes(3)));
        db.Jobs.Add(Job(JobStatus.Completed, "g1", now.AddMinutes(4)));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var details = await DbHelper.CreateDashboardReadStorage()
            .GetLockedGroupByIdAsync("g1", TestContext.Current.CancellationToken);

        Assert.NotNull(details);
        Assert.Equal("g1", details.GroupId);
        Assert.Equal(4, details.Jobs.Count);
        Assert.Equal(LockedGroupJobRole.Locker, details.Jobs[0].Role);
        Assert.Equal(LockedGroupJobRole.Frozen, details.Jobs[1].Role);
        Assert.Equal(LockedGroupJobRole.WaitingPrev, details.Jobs[2].Role);
        Assert.Equal(LockedGroupJobRole.Scheduled, details.Jobs[3].Role);
    }
}