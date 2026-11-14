using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.IntegrationTests.Postgres.Helpers;
using Jobby.Postgres.Dashboard.Commands;
using Npgsql;

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

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task GetJobsAsync_FiltersByInclusiveScheduledBounds(bool useFrom, bool useTo)
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var from = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc).AddTicks(1234560);
        var to = from.AddHours(2);
        var rows = new[]
        {
            (Name: "before", Scheduled: from.AddTicks(-10)),
            (Name: "from", Scheduled: from),
            (Name: "middle", Scheduled: from.AddHours(1)),
            (Name: "to", Scheduled: to),
            (Name: "after", Scheduled: to.AddTicks(10)),
        };
        foreach (var row in rows)
        {
            var job = NewJob(JobStatus.Scheduled, row.Name);
            job.ScheduledStartAt = row.Scheduled;
            job.CreatedAt = from.AddYears(-1);
            db.Jobs.Add(job);
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var page = await DbHelper.CreateDashboardReadStorage().GetJobsAsync(
            new DashboardJobsQuery
            {
                ScheduledFrom = useFrom ? from : null,
                ScheduledTo = useTo ? to : null,
            }, TestContext.Current.CancellationToken);

        var expected = rows.Where(row => (!useFrom || row.Scheduled >= from) && (!useTo || row.Scheduled <= to))
            .Select(row => row.Name).ToArray();
        Assert.Equal(expected.Length, page.TotalCount);
        Assert.Equal(expected, page.Items.Select(item => item.JobName));
    }

    [Fact]
    public async Task GetJobsAsync_ReversedScheduledBounds_ReturnNoMatches()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var scheduled = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var job = NewJob(JobStatus.Scheduled, "job");
        job.ScheduledStartAt = scheduled;
        db.Jobs.Add(job);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var page = await DbHelper.CreateDashboardReadStorage().GetJobsAsync(
            new DashboardJobsQuery { ScheduledFrom = scheduled.AddHours(1), ScheduledTo = scheduled },
            TestContext.Current.CancellationToken);

        Assert.Equal(0, page.TotalCount);
        Assert.Empty(page.Items);
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
    public async Task GetJobsAsync_DefaultSort_IsScheduledAscending_WithStableIdTiesAcrossPages()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var scheduled = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc).AddTicks(1234560);
        var early = NewJob(JobStatus.Scheduled, "early");
        early.ScheduledStartAt = scheduled.AddHours(-1);
        early.CreatedAt = scheduled.AddDays(2);
        var tieLow = NewJob(JobStatus.Scheduled, "tie-low");
        tieLow.Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        tieLow.ScheduledStartAt = scheduled;
        tieLow.CreatedAt = scheduled.AddDays(1);
        var tieHigh = NewJob(JobStatus.Scheduled, "tie-high");
        tieHigh.Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
        tieHigh.ScheduledStartAt = scheduled;
        tieHigh.CreatedAt = scheduled.AddDays(-1);
        var late = NewJob(JobStatus.Scheduled, "late");
        late.ScheduledStartAt = scheduled.AddHours(1);
        late.CreatedAt = scheduled.AddDays(-2);
        db.Jobs.AddRange(late, tieHigh, early, tieLow);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var firstPage = await storage.GetJobsAsync(new DashboardJobsQuery { PageSize = 2 },
            TestContext.Current.CancellationToken);
        var secondPage = await storage.GetJobsAsync(new DashboardJobsQuery { PageSize = 2, Page = 2 },
            TestContext.Current.CancellationToken);
        Assert.Equal(4, firstPage.TotalCount);
        Assert.Equal(new[] { "early", "tie-low", "tie-high", "late" },
            firstPage.Items.Concat(secondPage.Items).Select(item => item.JobName));

        var descending = await storage.GetJobsAsync(new DashboardJobsQuery { SortDescending = true },
            TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "late", "tie-high", "tie-low", "early" },
            descending.Items.Select(item => item.JobName));

        var created = await storage.GetJobsAsync(new DashboardJobsQuery { SortBy = DashboardJobsSortBy.CreatedAt },
            TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "late", "tie-high", "tie-low", "early" },
            created.Items.Select(item => item.JobName));
    }

    [Fact]
    public async Task GetJobsAsync_CombinedFilters_AlignParametersCorrectly()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var scheduled = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var match = NewJob(JobStatus.Failed, "send-report");
        match.QueueName = "emails";
        match.ScheduledStartAt = scheduled.AddHours(-1);
        var wrongStatus = NewJob(JobStatus.Scheduled, "send-report-2");
        wrongStatus.QueueName = "emails";
        wrongStatus.ScheduledStartAt = scheduled.AddHours(-1);
        var wrongQueue = NewJob(JobStatus.Failed, "send-report-3");
        wrongQueue.QueueName = "default";
        wrongQueue.ScheduledStartAt = scheduled.AddHours(-1);
        var wrongName = NewJob(JobStatus.Failed, "cleanup");
        wrongName.QueueName = "emails";
        wrongName.ScheduledStartAt = scheduled.AddHours(-1);
        var tooOld = NewJob(JobStatus.Failed, "send-report-old");
        tooOld.QueueName = "emails";
        tooOld.ScheduledStartAt = scheduled.AddDays(-10);
        db.Jobs.AddRange(match, wrongStatus, wrongQueue, wrongName, tooOld);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var page = await storage.GetJobsAsync(
            new DashboardJobsQuery
            {
                Status = JobStatus.Failed,
                QueueName = "emails",
                JobNameSearch = "send",
                ScheduledFrom = scheduled.AddDays(-2),
                ScheduledTo = scheduled,
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
    public async Task GetJobsAsync_DefaultSort_MergesQueuesAndStatusesIntoOneScheduledOrder()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var start = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var rows = new[]
        {
            (Name: "a", Queue: "alpha", Status: JobStatus.Scheduled),
            (Name: "b", Queue: "beta", Status: JobStatus.Failed),
            (Name: "c", Queue: "alpha", Status: JobStatus.Failed),
            (Name: "d", Queue: "gamma", Status: JobStatus.Processing),
            (Name: "e", Queue: "beta", Status: JobStatus.Scheduled),
            (Name: "f", Queue: "alpha", Status: JobStatus.Scheduled),
            (Name: "g", Queue: "gamma", Status: JobStatus.WaitingPrev),
            (Name: "h", Queue: "beta", Status: JobStatus.Completed),
            (Name: "i", Queue: "alpha", Status: JobStatus.Frozen),
        };
        for (var i = 0; i < rows.Length; i++)
        {
            var job = NewJob(rows[i].Status, rows[i].Name);
            job.QueueName = rows[i].Queue;
            job.ScheduledStartAt = start.AddMinutes(i + 1);
            db.Jobs.Add(job);
        }

        var recurrent = Recurrent("recurrent");
        recurrent.QueueName = "alpha";
        recurrent.ScheduledStartAt = start;
        db.Jobs.Add(recurrent);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var pages = new List<PagedResult<DashboardJobListItemDto>>();
        for (var page = 1; page <= 3; page++)
        {
            pages.Add(await storage.GetJobsAsync(new DashboardJobsQuery { Page = page, PageSize = 4 },
                TestContext.Current.CancellationToken));
        }

        Assert.All(pages, page => Assert.Equal(9, page.TotalCount));
        Assert.Equal(new[] { "a", "b", "c", "d" }, pages[0].Items.Select(item => item.JobName));
        Assert.Equal(new[] { "e", "f", "g", "h" }, pages[1].Items.Select(item => item.JobName));
        Assert.Equal(new[] { "i" }, pages[2].Items.Select(item => item.JobName));

        var descending = await storage.GetJobsAsync(
            new DashboardJobsQuery { PageSize = 4, SortDescending = true }, TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "i", "h", "g", "f" }, descending.Items.Select(item => item.JobName));
    }

    [Fact]
    public async Task GetJobsAsync_DuplicateStatuses_ReturnEachJobOnce()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var start = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var names = new[] { "s1", "f1", "s2", "s3" };
        for (var i = 0; i < names.Length; i++)
        {
            var job = NewJob(names[i].StartsWith('s') ? JobStatus.Scheduled : JobStatus.Failed, names[i]);
            job.ScheduledStartAt = start.AddMinutes(i);
            db.Jobs.Add(job);
        }

        db.Jobs.Add(NewJob(JobStatus.Completed, "completed"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var page = await DbHelper.CreateDashboardReadStorage().GetJobsAsync(
            new DashboardJobsQuery { Statuses = [JobStatus.Scheduled, JobStatus.Failed, JobStatus.Scheduled] },
            TestContext.Current.CancellationToken);

        Assert.Equal(names, page.Items.Select(item => item.JobName));
        Assert.Equal(4, page.TotalCount);
    }

    [Fact]
    public async Task GetJobsAsync_QueueWithoutJobs_ReturnsEmptyPageAndZeroTotal()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.Add(NewJob(JobStatus.Scheduled, "other-queue"));
        var onlyRecurrent = Recurrent("only-recurrent");
        onlyRecurrent.QueueName = "recurrent-only";
        db.Jobs.Add(onlyRecurrent);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var storage = DbHelper.CreateDashboardReadStorage();

        var missing = await storage.GetJobsAsync(new DashboardJobsQuery { QueueName = "missing" },
            TestContext.Current.CancellationToken);
        var recurrentOnly = await storage.GetJobsAsync(new DashboardJobsQuery { QueueName = "recurrent-only" },
            TestContext.Current.CancellationToken);

        Assert.Empty(missing.Items);
        Assert.Equal(0, missing.TotalCount);
        Assert.Empty(recurrentOnly.Items);
        Assert.Equal(0, recurrentOnly.TotalCount);
    }

    [Fact]
    public async Task GetJobsAsync_PagesAroundPartitionedReadLimit_ContinueTheSameOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var start = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        const int pageSize = 50;
        const int defaultQueueJobs = GetJobsPagedCommand.MaxPartitionedRows + 10;
        await using (var conn = await DbHelper.DataSource.OpenConnectionAsync(cancellationToken))
        await using (var insert = new NpgsqlCommand(
                         @"INSERT INTO jobby_jobs (id, job_name, status, created_at, scheduled_start_at, queue_name)
                           SELECT gen_random_uuid(), 'job-' || lpad(i::text, 6, '0'), 1, $1,
                                  $1 + interval '1 second' * i, 'default'
                           FROM generate_series(1, $2) i", conn))
        {
            insert.Parameters.Add(new NpgsqlParameter { Value = start });
            insert.Parameters.Add(new NpgsqlParameter { Value = defaultQueueJobs });
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        // Two more queues turn the unfiltered list into three partitions; their jobs sort before 'default'.
        var first = NewJob(JobStatus.Scheduled, "first");
        first.QueueName = "a-first";
        first.ScheduledStartAt = start.AddMinutes(-2);
        var second = NewJob(JobStatus.Scheduled, "second");
        second.QueueName = "z-second";
        second.ScheduledStartAt = start.AddMinutes(-1);
        db.Jobs.AddRange(first, second);
        await db.SaveChangesAsync(cancellationToken);
        var storage = DbHelper.CreateDashboardReadStorage();

        // One partition: offset + page size reaches the limit exactly on the first page and exceeds it on the next.
        var lastPartitionedPage = GetJobsPagedCommand.MaxPartitionedRows / pageSize;
        foreach (var page in new[] { lastPartitionedPage, lastPartitionedPage + 1 })
        {
            var result = await storage.GetJobsAsync(
                new DashboardJobsQuery { QueueName = "default", Page = page, PageSize = pageSize }, cancellationToken);

            var firstJob = (page - 1) * pageSize + 1;
            Assert.Equal(JobNames(firstJob, Math.Min(pageSize, defaultQueueJobs - firstJob + 1)),
                result.Items.Select(item => item.JobName));
            Assert.Equal(defaultQueueJobs, result.TotalCount);
        }

        // Three partitions: the same limit is crossed at a third of the offset.
        var lastPartitionedOffset = (GetJobsPagedCommand.MaxPartitionedRows / 3 - pageSize) / pageSize * pageSize;
        foreach (var offset in new[] { lastPartitionedOffset, lastPartitionedOffset + pageSize })
        {
            var result = await storage.GetJobsAsync(
                new DashboardJobsQuery { Page = offset / pageSize + 1, PageSize = pageSize }, cancellationToken);

            // 'first' and 'second' occupy the first two positions of the merged order.
            Assert.Equal(JobNames(offset - 1, pageSize), result.Items.Select(item => item.JobName));
            Assert.Equal(defaultQueueJobs + 2, result.TotalCount);
        }
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

    private static IEnumerable<string> JobNames(int first, int count) =>
        Enumerable.Range(first, count).Select(i => $"job-{i:D6}");

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