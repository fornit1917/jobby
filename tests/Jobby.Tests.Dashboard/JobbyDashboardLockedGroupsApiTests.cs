using System.Net;
using System.Net.Http.Json;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard;
using Jobby.Dashboard.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobby.Tests.Dashboard;

public class JobbyDashboardLockedGroupsApiTests
{
    [Fact]
    public async Task GetLockedGroups_ReturnsPagedResult_AndForwardsPaging()
    {
        var reader = new FakeDashboardDataReader
        {
            ManagementEnabled = true,
            LockedGroups = new PagedResult<LockedGroupDto>(Items: [], TotalCount: 0, Page: 2, PageSize: 10),
        };
        await using var app = await StartAsync(reader);

        var resp = await app.GetTestClient().GetAsync("/api/locked-groups?page=2&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(2, reader.LastLockedGroupsQuery!.Page);
        Assert.Equal(10, reader.LastLockedGroupsQuery!.PageSize);
    }

    [Fact]
    public async Task GetLockedGroupDetail_Returns404_WhenNotLocked()
    {
        var reader = new FakeDashboardDataReader { ManagementEnabled = true, LockedGroupDetails = null };
        await using var app = await StartAsync(reader);

        var resp = await app.GetTestClient().GetAsync("/api/locked-groups/detail?groupId=g1");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Unlock_WhenInserted_Returns200WithInsertedTrue_AndForwardsGroupId()
    {
        var reader = new FakeDashboardDataReader { ManagementEnabled = true, UnlockInserted = true };
        await using var app = await StartAsync(reader);

        var resp = await SendManageAsync(app, HttpMethod.Post, "/api/locked-groups/unlock-request",
            r => r.Content = JsonContent.Create(new UnlockGroupRequest("g1")));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<RequestGroupUnlockResultDto>();
        Assert.True(dto!.Inserted);
        Assert.Equal("g1", reader.LastUnlockGroupId);
    }

    [Fact]
    public async Task Unlock_WhenNoOp_Returns200WithInsertedFalse_NotNotFound()
    {
        var reader = new FakeDashboardDataReader { ManagementEnabled = true, UnlockInserted = false };
        await using var app = await StartAsync(reader);

        var resp = await SendManageAsync(app, HttpMethod.Post, "/api/locked-groups/unlock-request",
            r => r.Content = JsonContent.Create(new UnlockGroupRequest("g1")));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<RequestGroupUnlockResultDto>();
        Assert.False(dto!.Inserted);
    }

    [Fact]
    public async Task Unlock_WhenManagementDisabled_Returns404()
    {
        var reader = new FakeDashboardDataReader { ManagementEnabled = false };
        await using var app = await StartAsync(reader);

        var resp = await SendManageAsync(app, HttpMethod.Post, "/api/locked-groups/unlock-request",
            r => r.Content = JsonContent.Create(new UnlockGroupRequest("g1")));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Null(reader.LastUnlockGroupId);
    }

    [Fact]
    public async Task Unlock_WithoutCsrfHeader_Returns403_AndDoesNotAct()
    {
        var reader = new FakeDashboardDataReader { ManagementEnabled = true };
        await using var app = await StartAsync(reader);

        var resp = await app.GetTestClient().PostAsync("/api/locked-groups/unlock-request",
            JsonContent.Create(new UnlockGroupRequest("g1")));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Null(reader.LastUnlockGroupId);
    }

    private static async Task<WebApplication> StartAsync(FakeDashboardDataReader reader)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(new JobbyDashboardOptions());
        builder.Services.AddAntiforgery(o => o.HeaderName = JobbyDashboardProtocol.ManagementRequestHeader);
        builder.Services.AddScoped<IDashboardDataReader>(_ => reader);
        var state = new JobbyDashboardAuthState();
        state.Configure(JobbyDashboardAuthMode.Anonymous);
        var app = builder.Build();
        JobbyDashboardApi.MapAll(app, state);
        await app.StartAsync();
        return app;
    }

    private static async Task<HttpResponseMessage> SendManageAsync(WebApplication app, HttpMethod method, string url,
        Action<HttpRequestMessage>? configure = null)
    {
        var client = app.GetTestClient();
        var configResponse = await client.GetAsync("/api/config");
        configResponse.EnsureSuccessStatusCode();
        var dto = await configResponse.Content.ReadFromJsonAsync<JobbyDashboardClientConfigDto>();
        var cookieHeader = string.Empty;
        if (configResponse.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            cookieHeader = string.Join("; ", setCookies.Select(c => c.Split(';', 2)[0]));
        }

        var request = new HttpRequestMessage(method, url);
        configure?.Invoke(request);
        request.Headers.Add(dto!.ManagementRequestHeaderName!, dto.ManagementRequestToken!);
        if (!string.IsNullOrEmpty(cookieHeader))
        {
            request.Headers.Add("Cookie", cookieHeader);
        }

        return await client.SendAsync(request);
    }
}
