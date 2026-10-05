using System.Net;
using System.Net.Http.Json;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard;
using Jobby.Dashboard.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobby.Tests.Dashboard;

public class JobbyDashboardManagementApiTests
{
    private sealed record ManagementRequestToken(string HeaderName, string RequestToken, string CookieHeader);

    private static async Task<ManagementRequestToken> GetManagementRequestTokenAsync(
        HttpClient client,
        Action<HttpRequestMessage>? configure = null)
    {
        var configRequest = new HttpRequestMessage(HttpMethod.Get, "/api/config");
        configure?.Invoke(configRequest);
        using var configResponse = await client.SendAsync(configRequest);
        configResponse.EnsureSuccessStatusCode();

        var dto = await configResponse.Content.ReadFromJsonAsync<JobbyDashboardClientConfigDto>();
        if (string.IsNullOrWhiteSpace(dto?.ManagementRequestHeaderName) ||
            string.IsNullOrWhiteSpace(dto.ManagementRequestToken))
        {
            throw new InvalidOperationException("The dashboard config did not return a management request token.");
        }

        var cookieHeader = string.Empty;
        if (configResponse.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            cookieHeader = string.Join("; ", setCookies.Select(c => c.Split(';', 2)[0]));
        }

        return new ManagementRequestToken(dto.ManagementRequestHeaderName, dto.ManagementRequestToken, cookieHeader);
    }

    private static async Task<HttpResponseMessage> SendManageAsync(
        WebApplication app,
        HttpMethod method,
        string url,
        Action<HttpRequestMessage>? configure = null)
    {
        var client = app.GetTestClient();
        var token = await GetManagementRequestTokenAsync(client, configure);
        var request = new HttpRequestMessage(method, url);
        configure?.Invoke(request);
        request.Headers.Add(token.HeaderName, token.RequestToken);
        if (!string.IsNullOrEmpty(token.CookieHeader))
        {
            request.Headers.Add("Cookie", token.CookieHeader);
        }

        return await client.SendAsync(request);
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

    [Fact]
    public async Task Trigger_WhenManagementEnabledAndJobAffected_Returns204AndForwardsId()
    {
        var reader = new FakeDashboardDataReader { ManagementEnabled = true, ManagementAffected = 1 };
        await using var app = await StartAsync(reader);
        var id = Guid.NewGuid();

        var resp = await SendManageAsync(app, HttpMethod.Post, $"/api/jobs/{id}/trigger");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Equal([id], reader.LastTriggerIds);
    }

    [Fact]
    public async Task Trigger_WhenNoJobAffected_Returns404()
    {
        var reader = new FakeDashboardDataReader { ManagementEnabled = true, ManagementAffected = 0 };
        await using var app = await StartAsync(reader);

        var resp = await SendManageAsync(app, HttpMethod.Post, $"/api/jobs/{Guid.NewGuid()}/trigger");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Trigger_WithoutCsrfHeader_IsRejectedAndDoesNotAct()
    {
        var reader = new FakeDashboardDataReader { ManagementEnabled = true, ManagementAffected = 1 };
        await using var app = await StartAsync(reader);

        var resp = await app.GetTestClient().PostAsync($"/api/jobs/{Guid.NewGuid()}/trigger", null);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Null(reader.LastTriggerIds);
    }

    [Fact]
    public async Task Trigger_WithInvalidCsrfToken_IsRejectedAndDoesNotAct()
    {
        var reader = new FakeDashboardDataReader { ManagementEnabled = true, ManagementAffected = 1 };
        await using var app = await StartAsync(reader);
        var client = app.GetTestClient();
        var token = await GetManagementRequestTokenAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/jobs/{Guid.NewGuid()}/trigger");
        request.Headers.Add(token.HeaderName, "bad-token");
        if (!string.IsNullOrEmpty(token.CookieHeader))
        {
            request.Headers.Add("Cookie", token.CookieHeader);
        }

        var resp = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Null(reader.LastTriggerIds);
    }

    [Fact]
    public async Task Trigger_WhenManagementDisabled_Returns404()
    {
        var reader = new FakeDashboardDataReader { ManagementEnabled = false };
        await using var app = await StartAsync(reader);

        var resp = await SendManageAsync(app, HttpMethod.Post, $"/api/jobs/{Guid.NewGuid()}/trigger");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Null(reader.LastTriggerIds);
    }

    [Fact]
    public async Task Trigger_WhenManagementDisabledWithoutCsrfHeader_Returns404AndDoesNotAct()
    {
        var reader = new FakeDashboardDataReader { ManagementEnabled = false };
        await using var app = await StartAsync(reader);

        var resp = await app.GetTestClient().PostAsync($"/api/jobs/{Guid.NewGuid()}/trigger", null);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Null(reader.LastTriggerIds);
    }

    [Fact]
    public async Task Retry_WhenManagementEnabledAndJobAffected_Returns204AndForwardsId()
    {
        var reader = new FakeDashboardDataReader { ManagementEnabled = true, ManagementAffected = 1 };
        await using var app = await StartAsync(reader);
        var id = Guid.NewGuid();

        var resp = await SendManageAsync(app, HttpMethod.Post, $"/api/jobs/{id}/retry");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Equal([id], reader.LastRetryIds);
    }

    [Fact]
    public async Task DeleteJob_WhenManagementEnabledAndJobAffected_Returns204AndForwardsId()
    {
        var reader = new FakeDashboardDataReader { ManagementEnabled = true, ManagementAffected = 1 };
        await using var app = await StartAsync(reader);
        var id = Guid.NewGuid();

        var resp = await SendManageAsync(app, HttpMethod.Delete, $"/api/jobs/{id}");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Equal([id], reader.LastDeleteIds);
    }

    [Fact]
    public async Task DeleteRecurrent_WhenManagementEnabledAndJobAffected_Returns204AndForwardsId()
    {
        var reader = new FakeDashboardDataReader { ManagementEnabled = true, ManagementAffected = 1 };
        await using var app = await StartAsync(reader);
        var id = Guid.NewGuid();

        var resp = await SendManageAsync(app, HttpMethod.Delete, $"/api/recurrent/{id}");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Equal([id], reader.LastDeleteRecurrentIds);
    }

    [Fact]
    public async Task DeleteRecurrent_WhenManagementDisabled_Returns404()
    {
        var reader = new FakeDashboardDataReader { ManagementEnabled = false };
        await using var app = await StartAsync(reader);

        var resp = await SendManageAsync(app, HttpMethod.Delete, $"/api/recurrent/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Null(reader.LastDeleteRecurrentIds);
    }

    [Fact]
    public async Task Trigger_InPolicyMode_RejectsUnauthenticatedAndAllowsAuthenticated()
    {
        var reader = new FakeDashboardDataReader { ManagementEnabled = true, ManagementAffected = 1 };
        await using var app = await StartPolicyAsync(reader);
        var id = Guid.NewGuid();

        var anonymous = await app.GetTestClient().PostAsync($"/api/jobs/{id}/trigger", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Null(reader.LastTriggerIds);

        var authedResp = await SendManageAsync(app, HttpMethod.Post, $"/api/jobs/{id}/trigger",
            request => request.Headers.Add("X-Test-Token", "good"));
        Assert.Equal(HttpStatusCode.NoContent, authedResp.StatusCode);
        Assert.Equal([id], reader.LastTriggerIds);
    }

    private static async Task<WebApplication> StartPolicyAsync(FakeDashboardDataReader reader)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(new JobbyDashboardOptions());
        builder.Services.AddAntiforgery(o => o.HeaderName = JobbyDashboardProtocol.ManagementRequestHeader);
        builder.Services.AddScoped<IDashboardDataReader>(_ => reader);
        builder.Services.AddAuthentication(TestAuthHandler.Scheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(JobbyDashboardPolicies.Read, p => p.RequireAuthenticatedUser())
            .AddPolicy(JobbyDashboardPolicies.Manage, p => p.RequireAuthenticatedUser());
        var state = new JobbyDashboardAuthState();
        state.Configure(JobbyDashboardAuthMode.Policy);
        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        JobbyDashboardApi.MapAll(app, state);
        await app.StartAsync();
        return app;
    }
}
