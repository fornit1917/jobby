using System.Net;
using System.Net.Http.Json;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobby.Tests.Dashboard;

public class JobbyDashboardApiTests
{
    private static async Task<WebApplication> StartAsync(FakeDashboardDataReader reader)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(new JobbyDashboardOptions { StaleServerThresholdSeconds = 123 });
        builder.Services.AddAntiforgery(o => o.HeaderName = JobbyDashboardProtocol.ManagementRequestHeader);
        builder.Services.AddScoped<IDashboardDataReader>(_ => reader);
        var state = new JobbyDashboardAuthState();
        state.Configure(JobbyDashboardAuthMode.Anonymous);
        var app = builder.Build();
        JobbyDashboardApi.MapAll(app, state);
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Stats_ReturnsOk()
    {
        await using var app = await StartAsync(new FakeDashboardDataReader());
        Assert.Equal(HttpStatusCode.OK, (await app.GetTestClient().GetAsync("/api/stats")).StatusCode);
    }

    [Fact]
    public async Task Config_ReturnsSettingsFromOptions()
    {
        await using var app = await StartAsync(new FakeDashboardDataReader());
        var dto = await app.GetTestClient()
            .GetFromJsonAsync<JobbyDashboardClientConfigDto>("/api/config");
        Assert.Equal(123, dto!.StaleServerThresholdSeconds);
        Assert.False(dto.ManagementEnabled);
        Assert.Equal(JobbyDashboardProtocol.ManagementRequestHeader, dto.ManagementRequestHeaderName);
        Assert.False(string.IsNullOrWhiteSpace(dto.ManagementRequestToken));
    }

    [Fact]
    public async Task Jobs_ParsesPagingFromQueryString()
    {
        var reader = new FakeDashboardDataReader();
        await using var app = await StartAsync(reader);
        Assert.Equal(HttpStatusCode.OK, (await app.GetTestClient().GetAsync("/api/jobs?page=3&pageSize=7")).StatusCode);
        Assert.Equal(3, reader.LastJobsQuery!.Page);
        Assert.Equal(7, reader.LastJobsQuery!.PageSize);
        Assert.Equal(DashboardJobsSortBy.ScheduledStartAt, reader.LastJobsQuery.SortBy);
        Assert.False(reader.LastJobsQuery.SortDescending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Jobs_ParsesScheduledBoundsAsUtc_AndPreservesExplicitSort(bool reversed)
    {
        var reader = new FakeDashboardDataReader();
        await using var app = await StartAsync(reader);
        var from = new DateTimeOffset(2026, 6, 10, 12, 30, 0, TimeSpan.FromHours(3));
        var to = from.AddDays(reversed ? -1 : 1);
        var url = $"/api/jobs?scheduledFrom={Uri.EscapeDataString(from.ToString("O"))}" +
                  $"&scheduledTo={Uri.EscapeDataString(to.ToString("O"))}&sortBy=CreatedAt&sortDescending=true";

        using var response = await app.GetTestClient().GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(from.UtcDateTime, reader.LastJobsQuery!.ScheduledFrom);
        Assert.Equal(to.UtcDateTime, reader.LastJobsQuery.ScheduledTo);
        Assert.Equal(DateTimeKind.Utc, reader.LastJobsQuery.ScheduledFrom?.Kind);
        Assert.Equal(DateTimeKind.Utc, reader.LastJobsQuery.ScheduledTo?.Kind);
        Assert.Equal(DashboardJobsSortBy.CreatedAt, reader.LastJobsQuery.SortBy);
        Assert.True(reader.LastJobsQuery.SortDescending);
    }

    [Fact]
    public async Task Jobs_IgnoresMalformedBounds_AndDefaultsMalformedSort()
    {
        var reader = new FakeDashboardDataReader();
        await using var app = await StartAsync(reader);

        using var response = await app.GetTestClient().GetAsync(
            "/api/jobs?scheduledFrom=invalid&scheduledTo=invalid&sortBy=invalid&sortDescending=invalid",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(reader.LastJobsQuery!.ScheduledFrom);
        Assert.Null(reader.LastJobsQuery.ScheduledTo);
        Assert.Equal(DashboardJobsSortBy.ScheduledStartAt, reader.LastJobsQuery.SortBy);
        Assert.False(reader.LastJobsQuery.SortDescending);
    }

    [Fact]
    public async Task Jobs_ParsesMultipleStatuses_AndDropsUndefinedTokens()
    {
        var reader = new FakeDashboardDataReader();
        await using var app = await StartAsync(reader);
        var client = app.GetTestClient();

        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/api/jobs?statuses=Scheduled,Processing,WaitingPrev,Frozen")).StatusCode);
        Assert.Equal(
            [JobStatus.Scheduled, JobStatus.Processing, JobStatus.WaitingPrev, JobStatus.Frozen],
            reader.LastJobsQuery!.Statuses);

        await client.GetAsync("/api/jobs?statuses=Scheduled,999,Frozon");
        Assert.Equal([JobStatus.Scheduled], reader.LastJobsQuery!.Statuses);
    }

    [Fact]
    public async Task JobById_NotFound_Returns404()
    {
        await using var app = await StartAsync(new FakeDashboardDataReader { JobById = null });
        Assert.Equal(HttpStatusCode.NotFound,
            (await app.GetTestClient().GetAsync($"/api/jobs/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Config_IncludesDateTimeDefaultsAndServerTime()
    {
        var before = DateTime.UtcNow;
        await using var app = await StartAsync(new FakeDashboardDataReader());
        var dto = await app.GetTestClient()
            .GetFromJsonAsync<JobbyDashboardClientConfigDto>("/api/config");

        Assert.Equal(TimeZoneInfo.Utc.Id, dto!.DefaultTimeZoneId);
        Assert.Equal(DashboardDateStyle.Iso, dto.DefaultDateStyle);
        Assert.Equal(DashboardTimeStyle.H24WithSeconds, dto.DefaultTimeStyle);

        Assert.InRange(dto.ServerTimeUtc, before.AddSeconds(-5), DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task Config_IncludesDefaultLanguage()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(new JobbyDashboardOptions { DefaultLanguage = DashboardLanguage.Russian });
        builder.Services.AddAntiforgery(o => o.HeaderName = JobbyDashboardProtocol.ManagementRequestHeader);
        builder.Services.AddScoped<IDashboardDataReader>(_ => new FakeDashboardDataReader());
        var state = new JobbyDashboardAuthState();
        state.Configure(JobbyDashboardAuthMode.Anonymous);
        await using var app = builder.Build();
        JobbyDashboardApi.MapAll(app, state);
        await app.StartAsync();

        var dto = await app.GetTestClient()
            .GetFromJsonAsync<JobbyDashboardClientConfigDto>("/api/config");
        Assert.Equal(DashboardLanguage.Russian, dto!.DefaultLanguage);
    }

    [Fact]
    public async Task Config_DropsUnresolvableDefaultTimeZone()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(new JobbyDashboardOptions
        {
            DefaultTimeZone = TimeZoneInfo.CreateCustomTimeZone(
                "Not/A_Zone",
                TimeSpan.Zero,
                "Not a real zone",
                "Not a real zone")
        });
        builder.Services.AddAntiforgery(o => o.HeaderName = JobbyDashboardProtocol.ManagementRequestHeader);
        builder.Services.AddScoped<IDashboardDataReader>(_ => new FakeDashboardDataReader());
        var state = new JobbyDashboardAuthState();
        state.Configure(JobbyDashboardAuthMode.Anonymous);
        await using var app = builder.Build();
        JobbyDashboardApi.MapAll(app, state);
        await app.StartAsync();

        var dto = await app.GetTestClient()
            .GetFromJsonAsync<JobbyDashboardClientConfigDto>("/api/config");
        Assert.Null(dto!.DefaultTimeZoneId);
    }
}
