using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.IntegrationTests.Postgres.Helpers;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyDashboardReadStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class GetJobsTests
{
    [Fact]
    public async Task GetJobsAsync_FiltersByStatus_AndExcludesRecurrentTemplates()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.AddRange(
            NewJob(JobStatus.Failed, "a"),
            NewJob(JobStatus.Failed, "b"),
            NewJob(JobStatus.Scheduled, "c"),
            Recurrent("recurrent-x"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var page = await storage.GetJobsAsync(
            new DashboardJobsQuery { Status = JobStatus.Failed },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.All(page.Items, i => Assert.Equal(JobStatus.Failed, i.Status));
    }

    [Fact]
    public async Task GetJobsAsync_PagesAndReportsTotalCount()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        for (var i = 0; i < 5; i++)
        {
            db.Jobs.Add(NewJob(JobStatus.Scheduled, $"job-{i:D2}"));
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var page = await storage.GetJobsAsync(
            new DashboardJobsQuery
            {
                PageSize = 2, Page = 1,
                SortBy = DashboardJobsSortBy.JobName, SortDescending = false
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal("job-00", page.Items[0].JobName);
        Assert.Equal("job-01", page.Items[1].JobName);
    }

    [Fact]
    public async Task GetJobsAsync_SearchesByJobNameSubstring()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.AddRange(
            NewJob(JobStatus.Scheduled, "send-email"),
            NewJob(JobStatus.Scheduled, "send-sms"),
            NewJob(JobStatus.Scheduled, "cleanup"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var page = await storage.GetJobsAsync(
            new DashboardJobsQuery { JobNameSearch = "send" },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task GetJobsAsync_FiltersByQueueName()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var a = NewJob(JobStatus.Scheduled, "a");
        a.QueueName = "emails";
        var b = NewJob(JobStatus.Scheduled, "b");
        b.QueueName = "emails";
        var c = NewJob(JobStatus.Scheduled, "c");
        c.QueueName = "default";
        db.Jobs.AddRange(a, b, c);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var page = await storage.GetJobsAsync(
            new DashboardJobsQuery { QueueName = "emails" },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, i => Assert.Equal("emails", i.QueueName));
    }

    [Fact]
    public async Task GetJobsAsync_FiltersByCreatedDateRange()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var old = NewJob(JobStatus.Scheduled, "old");
        old.CreatedAt = DateTime.UtcNow.AddDays(-10);
        var recent = NewJob(JobStatus.Scheduled, "recent");
        recent.CreatedAt = DateTime.UtcNow;
        db.Jobs.AddRange(old, recent);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var page = await storage.GetJobsAsync(
            new DashboardJobsQuery { CreatedFrom = DateTime.UtcNow.AddDays(-1) },
            TestContext.Current.CancellationToken);

        Assert.Equal(1, page.TotalCount);
        Assert.Equal("recent", page.Items.Single().JobName);
    }

    [Fact]
    public async Task GetJobsAsync_OutOfRangePage_ReportsTotalCountWithEmptyItems()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.AddRange(
            NewJob(JobStatus.Scheduled, "a"),
            NewJob(JobStatus.Scheduled, "b"),
            NewJob(JobStatus.Scheduled, "c"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var page = await storage.GetJobsAsync(
            new DashboardJobsQuery { Page = 5, PageSize = 10 },
            TestContext.Current.CancellationToken);

        Assert.Equal(3, page.TotalCount);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task GetJobsAsync_FiltersByCreatedToUpperBound()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var older = NewJob(JobStatus.Scheduled, "older");
        older.CreatedAt = DateTime.UtcNow.AddDays(-3);
        var newer = NewJob(JobStatus.Scheduled, "newer");
        newer.CreatedAt = DateTime.UtcNow;
        db.Jobs.AddRange(older, newer);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var page = await storage.GetJobsAsync(
            new DashboardJobsQuery { CreatedTo = DateTime.UtcNow.AddDays(-1) },
            TestContext.Current.CancellationToken);

        Assert.Equal(1, page.TotalCount);
        Assert.Equal("older", page.Items.Single().JobName);
    }

    [Fact]
    public async Task GetJobsAsync_DefaultSort_IsCreatedAtDescending()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var first = NewJob(JobStatus.Scheduled, "first");
        first.CreatedAt = DateTime.UtcNow.AddMinutes(-10);
        var last = NewJob(JobStatus.Scheduled, "last");
        last.CreatedAt = DateTime.UtcNow;
        db.Jobs.AddRange(first, last);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var page = await storage.GetJobsAsync(new DashboardJobsQuery(), TestContext.Current.CancellationToken);

        Assert.Equal("last", page.Items[0].JobName);
        Assert.Equal("first", page.Items[1].JobName);
    }

    [Fact]
    public async Task GetJobsAsync_CombinedFilters_AlignParametersCorrectly()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var match = NewJob(JobStatus.Failed, "send-report");
        match.QueueName = "emails";
        match.CreatedAt = DateTime.UtcNow.AddHours(-1);
        var wrongStatus = NewJob(JobStatus.Scheduled, "send-report-2");
        wrongStatus.QueueName = "emails";
        wrongStatus.CreatedAt = DateTime.UtcNow.AddHours(-1);
        var wrongQueue = NewJob(JobStatus.Failed, "send-report-3");
        wrongQueue.QueueName = "default";
        wrongQueue.CreatedAt = DateTime.UtcNow.AddHours(-1);
        var wrongName = NewJob(JobStatus.Failed, "cleanup");
        wrongName.QueueName = "emails";
        wrongName.CreatedAt = DateTime.UtcNow.AddHours(-1);
        var tooOld = NewJob(JobStatus.Failed, "send-report-old");
        tooOld.QueueName = "emails";
        tooOld.CreatedAt = DateTime.UtcNow.AddDays(-10);
        db.Jobs.AddRange(match, wrongStatus, wrongQueue, wrongName, tooOld);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var page = await storage.GetJobsAsync(
            new DashboardJobsQuery
            {
                Status = JobStatus.Failed,
                QueueName = "emails",
                JobNameSearch = "send",
                CreatedFrom = DateTime.UtcNow.AddDays(-2),
                CreatedTo = DateTime.UtcNow,
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(1, page.TotalCount);
        Assert.Equal("send-report", page.Items.Single().JobName);
    }

    [Fact]
    public async Task GetJobsAsync_FiltersByQueueAndStatuses_ExcludesTerminalAndRecurrent_OrdersByScheduledAscending()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var t = DateTime.UtcNow;
        var s1 = NewJob(JobStatus.Scheduled, "s1");
        s1.QueueName = "emails";
        s1.ScheduledStartAt = t.AddMinutes(2);
        var p1 = NewJob(JobStatus.Processing, "p1");
        p1.QueueName = "emails";
        p1.ScheduledStartAt = t.AddMinutes(1);
        var done = NewJob(JobStatus.Completed, "done");
        done.QueueName = "emails";
        var other = NewJob(JobStatus.Scheduled, "other");
        other.QueueName = "default";
        var rec = Recurrent("rec");
        rec.QueueName = "emails";
        db.Jobs.AddRange(s1, p1, done, other, rec);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var page = await storage.GetJobsAsync(
            new DashboardJobsQuery
            {
                QueueName = "emails",
                Statuses = [JobStatus.Scheduled, JobStatus.Processing, JobStatus.WaitingPrev, JobStatus.Frozen],
                SortBy = DashboardJobsSortBy.ScheduledStartAt,
                SortDescending = false,
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { "p1", "s1" }, page.Items.Select(i => i.JobName).ToArray());
    }

    [Fact]
    public async Task GetJobsAsync_SetsIsGroupLocker_ForFailedGroupLocker()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var lockerId = Guid.NewGuid();
        db.Jobs.AddRange(
            new JobDbModel
            {
                Id = lockerId, JobName = "locker", Status = JobStatus.Failed, QueueName = "default",
                CreatedAt = DateTime.UtcNow, ScheduledStartAt = DateTime.UtcNow,
                SerializableGroupId = "g1", LockGroupIfFailed = true,
            },
            new JobDbModel
            {
                Id = Guid.NewGuid(), JobName = "plain", Status = JobStatus.Scheduled, QueueName = "default",
                CreatedAt = DateTime.UtcNow, ScheduledStartAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DbHelper.CreateDashboardReadStorage()
            .GetJobsAsync(new DashboardJobsQuery { PageSize = 50 }, TestContext.Current.CancellationToken);

        Assert.True(result.Items.Single(i => i.Id == lockerId).IsGroupLocker);
        Assert.False(result.Items.Single(i => i.JobName == "plain").IsGroupLocker);
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

    private static JobDbModel Recurrent(string name)
    {
        var j = NewJob(JobStatus.Scheduled, name);
        j.Schedule = "*/5 * * * * *";
        return j;
    }
}