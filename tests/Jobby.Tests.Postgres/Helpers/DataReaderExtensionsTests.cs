using Jobby.Core.Models;
using Jobby.Postgres.Helpers;

namespace Jobby.Tests.Postgres.Helpers;

public class DataReaderExtensionsTests
{
    private static readonly Dictionary<string, object?> FullJobRow = new()
    {
        ["id"] = Guid.NewGuid(),
        ["job_name"] = "TestJob",
        ["job_param"] = @"{""value"":1}",
        ["started_count"] = 3,
        ["schedule"] = "*/5 * * * *",
        ["next_job_id"] = Guid.NewGuid(),
        ["server_id"] = "server-1",
        ["scheduled_start_at"] = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        ["scheduler_type"] = "JOBBY_CRON",
    };

    private static Dictionary<string, object?> RowWithNulls() => new(FullJobRow)
    {
        ["job_param"] = null,
        ["schedule"] = null,
        ["next_job_id"] = null,
        ["scheduler_type"] = null,
    };

    [Fact]
    public async Task GetJobAsync_NoRows_ReturnsNull()
    {
        // covers both early exits at once: HasRows = false makes Read() return false too
        await using var reader = new FakeDbDataReader(FullJobRow, hasRows: false);

        var job = await reader.GetJobAsync();

        Assert.Null(job);
    }

    [Fact]
    public async Task GetJobAsync_MapsAllFields()
    {
        await using var reader = new FakeDbDataReader(FullJobRow);

        var job = await reader.GetJobAsync();

        Assert.NotNull(job);
        Assert.Equal(FullJobRow["id"], job!.Id);
        Assert.Equal("TestJob", job.JobName);
        Assert.Equal(FullJobRow["job_param"], job.JobParam);
        Assert.Equal(3, job.StartedCount);
        Assert.Equal("*/5 * * * *", job.Schedule);
        Assert.Equal(FullJobRow["next_job_id"], job.NextJobId);
        Assert.Equal("server-1", job.ServerId);
        Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc), job.ScheduledStartAt);
        Assert.Equal("JOBBY_CRON", job.SchedulerType);
    }

    [Fact]
    public async Task GetJobAsync_NullableColumnsWithNulls_MapsToNull()
    {
        await using var reader = new FakeDbDataReader(RowWithNulls());

        var job = await reader.GetJobAsync();

        Assert.NotNull(job);
        Assert.Null(job!.JobParam);
        Assert.Null(job.Schedule);
        Assert.Null(job.NextJobId);
        Assert.Null(job.SchedulerType);
    }

    [Fact]
    public async Task GetStuckJobAsync_MapsAllFields()
    {
        var row = new Dictionary<string, object?>
        {
            ["id"] = Guid.NewGuid(),
            ["job_name"] = "StuckJob",
            ["can_be_restarted"] = true,
            ["server_id"] = "lost-server",
        };
        await using var reader = new FakeDbDataReader(row);

        var stuckJob = await reader.GetStuckJobAsync();

        Assert.NotNull(stuckJob);
        Assert.Equal(row["id"], stuckJob!.Id);
        Assert.Equal("StuckJob", stuckJob.JobName);
        Assert.True(stuckJob.CanBeRestarted);
        Assert.Equal("lost-server", stuckJob.ServerId);
    }

    [Fact]
    public async Task GetStuckJobAsync_NoRows_ReturnsNull()
    {
        await using var reader = new FakeDbDataReader(new Dictionary<string, object?>(), hasRows: false);

        var stuckJob = await reader.GetStuckJobAsync();

        Assert.Null(stuckJob);
    }

    [Fact]
    public void GetNullableString_Value_ReturnsValue()
    {
        var row = new Dictionary<string, object?> { ["col"] = "value" };
        using var reader = new FakeDbDataReader(row);

        Assert.Equal("value", reader.GetNullableString("col"));
    }

    [Fact]
    public void GetNullableString_Null_ReturnsNull()
    {
        var row = new Dictionary<string, object?> { ["col"] = null };
        using var reader = new FakeDbDataReader(row);

        Assert.Null(reader.GetNullableString("col"));
    }

    [Fact]
    public void GetNullableDatetime_Value_ReturnsValue()
    {
        var dt = new DateTime(2026, 5, 5, 5, 5, 5, DateTimeKind.Utc);
        var row = new Dictionary<string, object?> { ["col"] = dt };
        using var reader = new FakeDbDataReader(row);

        Assert.Equal(dt, reader.GetNullableDatetime("col"));
    }

    [Fact]
    public void GetNullableDatetime_Null_ReturnsNull()
    {
        var row = new Dictionary<string, object?> { ["col"] = null };
        using var reader = new FakeDbDataReader(row);

        Assert.Null(reader.GetNullableDatetime("col"));
    }

    [Fact]
    public void GetNullableGuid_Value_ReturnsValue()
    {
        var guid = Guid.NewGuid();
        var row = new Dictionary<string, object?> { ["col"] = guid };
        using var reader = new FakeDbDataReader(row);

        Assert.Equal(guid, reader.GetNullableGuid("col"));
    }

    [Fact]
    public void GetNullableGuid_Null_ReturnsNull()
    {
        var row = new Dictionary<string, object?> { ["col"] = null };
        using var reader = new FakeDbDataReader(row);

        Assert.Null(reader.GetNullableGuid("col"));
    }
}
