using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.IntegrationTests.Postgres.Helpers;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyDashboardReadStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class GetRecurrentJobsTests
{
    [Fact]
    public async Task GetRecurrentJobsAsync_ReturnsOnlyRecurrentTemplates()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.AddRange(
            Recurrent("nightly", "0 0 * * *"),
            Recurrent("hourly", "0 * * * *"),
            NonRecurrent("one-off"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var page = await storage.GetRecurrentJobsAsync(
            new DashboardRecurrentJobsQuery(), TestContext.Current.CancellationToken);

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, i => Assert.False(string.IsNullOrEmpty(i.Schedule)));
        Assert.Contains(page.Items, i => i.JobName == "nightly" && i.Schedule == "0 0 * * *");
    }

    [Fact]
    public async Task GetRecurrentJobsAsync_SearchesByJobNameSubstring()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        db.Jobs.AddRange(
            Recurrent("report-daily", "0 0 * * *"),
            Recurrent("report-weekly", "0 0 * * 0"),
            Recurrent("cleanup", "0 * * * *"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var page = await storage.GetRecurrentJobsAsync(
            new DashboardRecurrentJobsQuery { JobNameSearch = "report" },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, i => Assert.Contains("report", i.JobName));
    }

    [Fact]
    public async Task GetRecurrentJobsAsync_PagesAndReportsTotalForOutOfRangePage()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        for (var i = 0; i < 3; i++)
        {
            db.Jobs.Add(Recurrent($"rec-{i:D2}", "0 0 * * *"));
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var firstPage = await storage.GetRecurrentJobsAsync(
            new DashboardRecurrentJobsQuery { Page = 1, PageSize = 2 },
            TestContext.Current.CancellationToken);
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Items.Count);

        var beyond = await storage.GetRecurrentJobsAsync(
            new DashboardRecurrentJobsQuery { Page = 5, PageSize = 2 },
            TestContext.Current.CancellationToken);
        Assert.Equal(3, beyond.TotalCount);
        Assert.Empty(beyond.Items);
    }

    private static JobDbModel Recurrent(string name, string schedule) => new()
    {
        Id = Guid.NewGuid(), JobName = name, Schedule = schedule,
        Status = JobStatus.Scheduled, QueueName = "default",
        CreatedAt = DateTime.UtcNow, ScheduledStartAt = DateTime.UtcNow.AddHours(1),
    };

    private static JobDbModel NonRecurrent(string name) => new()
    {
        Id = Guid.NewGuid(), JobName = name, Status = JobStatus.Scheduled,
        QueueName = "default", CreatedAt = DateTime.UtcNow, ScheduledStartAt = DateTime.UtcNow,
    };
}