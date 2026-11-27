using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Dashboard;
using Jobby.Core.Models.Dashboard;
using Jobby.Core.Services;
using Jobby.Dashboard;
using Jobby.Dashboard.Authorization.Basic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobby.Tests.Dashboard;

public class DashboardIntegrationTests
{
    private sealed class FakeReadStorage : IJobbyDashboardReadStorage
    {
        public Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DashboardStatsDto(StatusCounts: [], RecurrentStatusCounts: []));
        }

        public Task<PagedResult<DashboardJobListItemDto>> GetJobsAsync(DashboardJobsQuery query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PagedResult<DashboardJobListItemDto>(
                Items: [], TotalCount: 0, Page: 1, PageSize: 50));
        }

        public Task<DashboardJobDetailsDto?> GetJobByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DashboardJobDetailsDto?>(null);
        }

        public Task<PagedResult<DashboardRecurrentJobDto>> GetRecurrentJobsAsync(
            DashboardRecurrentJobsQuery query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PagedResult<DashboardRecurrentJobDto>(
                Items: [], TotalCount: 0, Page: 1, PageSize: 50));
        }

        public Task<IReadOnlyList<DashboardServerDto>> GetServersAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DashboardServerDto>>([]);
        }

        public Task<IReadOnlyList<DashboardQueueDto>> GetQueuesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DashboardQueueDto>>([]);
        }

        public Task<PagedResult<LockedGroupDto>> GetLockedGroupsAsync(LockedGroupsQuery query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PagedResult<LockedGroupDto>(
                Items: [], TotalCount: 0, Page: 1, PageSize: 50));
        }

        public Task<long> GetLockedGroupsCountAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0L);
        }

        public Task<LockedGroupDetailsDto?> GetLockedGroupByIdAsync(string groupId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<LockedGroupDetailsDto?>(null);
        }
    }

    // Its presence enables management; the tests using it never run a management action.
    private sealed class FakeCommandStorage : IJobbyDashboardCommandStorage
    {
        public Task<int> TriggerJobsAsync(IReadOnlyList<Guid> ids, DateTime now,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> RetryJobsAsync(IReadOnlyList<Guid> ids, DateTime now,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> DeleteJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> DeleteRecurrentJobsAsync(IReadOnlyList<Guid> ids,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> RequestGroupUnlockAsync(string groupId, DateTime now,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static async Task<WebApplication> StartAsync(Action<IJobbyDashboardBuilder> configureAuth)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IJobbyDashboardReadStorage>(new FakeReadStorage());
        configureAuth(builder.Services.AddJobbyDashboard());
        var app = builder.Build();
        app.MapJobbyDashboard("/jobby");
        await app.StartAsync();
        return app;
    }

    private static AuthenticationHeaderValue Basic(string u, string p)
        => new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{u}:{p}")));

    [Fact]
    public void SecureByDefault_NoAuthDecision_MapThrows()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IJobbyDashboardReadStorage>(new FakeReadStorage());
        builder.Services.AddJobbyDashboard();
        var app = builder.Build();
        Assert.Throws<InvalidOperationException>(() => app.MapJobbyDashboard("/jobby"));
    }

    [Fact]
    public void AddJobbyDashboard_CalledTwice_Throws()
    {
        var services = new ServiceCollection();
        services.AddJobbyDashboard();
        Assert.Throws<InvalidOperationException>(() => services.AddJobbyDashboard());
    }

    [Fact]
    public void AddJobbyDashboard_WhenHostHasNoAntiforgeryConfig_UsesFrameworkDefaultHeader()
    {
        var services = new ServiceCollection();
        var baseline = new ServiceCollection();
        baseline.AddAntiforgery();

        services.AddJobbyDashboard();

        using var provider = services.BuildServiceProvider();
        using var baselineProvider = baseline.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;
        var baselineOptions = baselineProvider.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;

        Assert.Equal(baselineOptions.HeaderName, options.HeaderName);
        Assert.NotEqual(JobbyDashboardProtocol.ManagementRequestHeader, options.HeaderName);
    }

    [Fact]
    public void AddJobbyDashboard_WhenHostConfiguredAntiforgery_PreservesHostHeader()
    {
        var services = new ServiceCollection();
        services.AddAntiforgery(o => o.HeaderName = "X-Host-Antiforgery");

        services.AddJobbyDashboard();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;
        Assert.Equal("X-Host-Antiforgery", options.HeaderName);
    }

    [Fact]
    public async Task Shell_ServedAtRoot_WithRewrittenBaseHref_AndWasmScript()
    {
        await using var app = await StartAsync(b => b.AllowAnonymous());
        var html = await app.GetTestClient().GetStringAsync("/jobby/");
        Assert.Contains("<base href=\"/jobby/\" />", html);
        Assert.Contains("blazor.webassembly.js", html);
        Assert.DoesNotContain("blazor.web.js", html);
        Assert.Contains("id=\"app\"", html);
    }

    [Fact]
    public async Task Framework_BootJson_IsServed()
    {
        await using var app = await StartAsync(b => b.AllowAnonymous());
        var resp = await app.GetTestClient().GetAsync("/jobby/_framework/blazor.boot.json");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Framework_Asset_RevalidatesWith304_OnMatchingETag()
    {
        await using var app = await StartAsync(b => b.AllowAnonymous());
        var client = app.GetTestClient();

        var first = await client.GetAsync("/jobby/_framework/blazor.boot.json");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.True(first.Headers.CacheControl?.NoCache);
        var etag = first.Headers.ETag;
        Assert.NotNull(etag);

        var conditional = new HttpRequestMessage(HttpMethod.Get, "/jobby/_framework/blazor.boot.json");
        conditional.Headers.IfNoneMatch.Add(etag);
        var second = await client.SendAsync(conditional);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task Framework_Asset_NegotiatesBrotli_AndDecodesToTheUncompressedAsset()
    {
        await using var app = await StartAsync(b => b.AllowAnonymous());
        var client = app.GetTestClient();

        var plain = await client.GetAsync("/jobby/_framework/blazor.boot.json");
        Assert.Equal(HttpStatusCode.OK, plain.StatusCode);
        Assert.Empty(plain.Content.Headers.ContentEncoding);
        var plainBytes = await plain.Content.ReadAsByteArrayAsync();

        var req = new HttpRequestMessage(HttpMethod.Get, "/jobby/_framework/blazor.boot.json");
        req.Headers.AcceptEncoding.ParseAdd("br");
        var br = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, br.StatusCode);
        Assert.Contains("br", br.Content.Headers.ContentEncoding);
        Assert.Contains("Accept-Encoding", br.Headers.Vary);
        Assert.Equal("application/json", br.Content.Headers.ContentType?.MediaType);

        var compressed = await br.Content.ReadAsByteArrayAsync();
        using var source = new MemoryStream(compressed);
        await using var brotli = new BrotliStream(source, CompressionMode.Decompress);
        using var decoded = new MemoryStream();
        await brotli.CopyToAsync(decoded);
        Assert.Equal(plainBytes, decoded.ToArray());
    }

    [Fact]
    public async Task DeepLink_FallsBackToShell()
    {
        await using var app = await StartAsync(b => b.AllowAnonymous());
        var html = await app.GetTestClient().GetStringAsync("/jobby/jobs");
        Assert.Contains("id=\"app\"", html);
    }

    [Fact]
    public async Task Anonymous_Api_Returns200()
    {
        await using var app = await StartAsync(b => b.AllowAnonymous());
        Assert.Equal(HttpStatusCode.OK, (await app.GetTestClient().GetAsync("/jobby/api/stats")).StatusCode);
    }

    [Theory]
    [InlineData(false, new[] { "SendReport" })]
    [InlineData(true, new string[0])]
    public async Task Config_ListsJobsOfTheHostAsCreatableUnlessTheDashboardIsReadOnly(bool readOnly,
        string[] expected)
    {
        var jobby = new JobbyBuilder();
        jobby.AddJob<SendReportCommand, SendReportHandler>();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IJobbyDashboardReadStorage>(new FakeReadStorage());
        builder.Services.AddSingleton<IJobbyDashboardCommandStorage>(new FakeCommandStorage());
        builder.Services.AddSingleton(jobby);
        builder.Services.AddSingleton<IJobbyClient>(new InMemoryJobbyClient(jobby.CreateJobsFactory()));
        builder.Services.AddJobbyDashboard(o => o.ReadOnly = readOnly).AllowAnonymous();
        await using var app = builder.Build();
        app.MapJobbyDashboard("/jobby");
        await app.StartAsync(TestContext.Current.CancellationToken);

        var dto = await app.GetTestClient().GetFromJsonAsync<JobbyDashboardClientConfigDto>(
            "/jobby/api/config", TestContext.Current.CancellationToken);

        Assert.Equal(expected, dto!.CreatableJobNames);
    }

    [Fact]
    public async Task Config_Anonymous_ReturnsSettings()
    {
        await using var app = await StartAsync(b => b.AllowAnonymous());
        var dto = await app.GetTestClient().GetFromJsonAsync<JobbyDashboardClientConfigDto>("/jobby/api/config");
        Assert.NotNull(dto);
        Assert.False(dto.ManagementEnabled);
        Assert.False(string.IsNullOrWhiteSpace(dto.ManagementRequestHeaderName));
        Assert.False(string.IsNullOrWhiteSpace(dto.ManagementRequestToken));
    }

    [Fact]
    public async Task Basic_ShellPublic_ApiProtected()
    {
        await using var app = await StartAsync(b => b.AddBasicAuth(o =>
        {
            o.Username = "admin";
            o.PasswordHash = PasswordHasher.Hash("s3cret");
        }));
        var client = app.GetTestClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/jobby/")).StatusCode);
        var api = await client.GetAsync("/jobby/api/stats");
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
        Assert.Contains(api.Headers.WwwAuthenticate, h => h.Scheme == "Basic");
    }

    [Fact]
    public async Task Basic_ValidCredentials_Returns200()
    {
        await using var app = await StartAsync(b => b.AddBasicAuth(o =>
        {
            o.Username = "admin";
            o.PasswordHash = PasswordHasher.Hash("s3cret");
        }));
        var req = new HttpRequestMessage(HttpMethod.Get, "/jobby/api/stats")
            { Headers = { Authorization = Basic("admin", "s3cret") } };
        Assert.Equal(HttpStatusCode.OK, (await app.GetTestClient().SendAsync(req)).StatusCode);
    }

    [Fact]
    public async Task Basic_WrongPassword_Returns401()
    {
        await using var app = await StartAsync(b => b.AddBasicAuth(o =>
        {
            o.Username = "admin";
            o.PasswordHash = PasswordHasher.Hash("s3cret");
        }));
        var req = new HttpRequestMessage(HttpMethod.Get, "/jobby/api/stats")
            { Headers = { Authorization = Basic("admin", "WRONG") } };
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetTestClient().SendAsync(req)).StatusCode);
    }

    [Fact]
    public async Task HostPolicy_WithRegisteredScheme_Enforced()
    {
        await using var app = await StartAsync(b =>
        {
            b.Services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, null);
            b.Services.AddAuthorization(o => o.AddPolicy("HostPolicy", p =>
                p.AddAuthenticationSchemes(TestAuthHandler.Scheme).RequireAuthenticatedUser()));
            b.RequireHostPolicy("HostPolicy");
        });
        var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/jobby/api/stats")).StatusCode);
        var req = new HttpRequestMessage(HttpMethod.Get, "/jobby/api/stats");
        req.Headers.Add("X-Test-Token", "good");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(req)).StatusCode);
    }

    [Fact]
    public async Task BasicPlusTestScheme_EitherAuthenticates()
    {
        await using var app = await StartAsync(b =>
        {
            b.AddBasicAuthScheme(o =>
            {
                o.Username = "admin";
                o.PasswordHash = PasswordHasher.Hash("s3cret");
            });
            b.Services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, null);
            b.RequireAuthorization(p => p
                .AddAuthenticationSchemes(JobbyDashboardBasicDefaults.Scheme, TestAuthHandler.Scheme)
                .RequireAuthenticatedUser());
        });
        var client = app.GetTestClient();

        var viaBasic = new HttpRequestMessage(HttpMethod.Get, "/jobby/api/stats")
            { Headers = { Authorization = Basic("admin", "s3cret") } };
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(viaBasic)).StatusCode);

        var viaTest = new HttpRequestMessage(HttpMethod.Get, "/jobby/api/stats");
        viaTest.Headers.Add("X-Test-Token", "good");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(viaTest)).StatusCode);

        var neither = await client.GetAsync("/jobby/api/stats");
        Assert.Equal(HttpStatusCode.Unauthorized, neither.StatusCode);
        var challenges = neither.Headers.WwwAuthenticate.Select(h => h.Scheme).ToList();
        Assert.Contains("Basic", challenges);
        Assert.Contains("Bearer", challenges);
    }

    [Fact]
    public async Task HostFallbackPolicy_ShellAndAssetsStayPublic_ApiProtected()
    {
        await using var app = await StartAsync(b =>
        {
            b.Services.AddAuthorization(o => o.FallbackPolicy =
                new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
            b.AddBasicAuth(o =>
            {
                o.Username = "admin";
                o.PasswordHash = PasswordHasher.Hash("s3cret");
            });
        });
        var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/jobby/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/jobby/_framework/blazor.boot.json")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/jobby/api/stats")).StatusCode);
    }

    [Fact]
    public async Task UnknownApiRoute_Returns404_NotShell()
    {
        await using var app = await StartAsync(b => b.AllowAnonymous());
        var resp = await app.GetTestClient().GetAsync("/jobby/api/not-real");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.DoesNotContain("id=\"app\"", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BarePrefix_NoTrailingSlash_RedirectsToTrailingSlash()
    {
        await using var app = await StartAsync(b => b.AllowAnonymous());
        var resp = await app.GetTestClient().GetAsync("/jobby");
        Assert.Equal(HttpStatusCode.Found, resp.StatusCode);
        Assert.Equal("/jobby/", resp.Headers.Location?.ToString());
    }

    private static WebApplicationBuilder CreateAnonymousDashboardBuilder()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IJobbyDashboardReadStorage>(new FakeReadStorage());
        builder.Services.AddJobbyDashboard().AllowAnonymous();
        return builder;
    }

    [Fact]
    public async Task MapJobbyDashboard_InAnApplicationWithoutWebApplication_ServesTheDashboard()
    {
        // The pipeline of an application with a Startup class: endpoints are added inside UseEndpoints.
        using var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<IJobbyDashboardReadStorage>(new FakeReadStorage());
                    services.AddJobbyDashboard().AllowAnonymous();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapJobbyDashboard("/jobby"));
                }))
            .StartAsync(TestContext.Current.CancellationToken);

        var response = await host.GetTestClient()
            .GetAsync("/jobby/api/stats", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/jobby/")]
    [InlineData("/jobby/jobs")]
    [InlineData("/jobby/api/stats")]
    [InlineData("/jobby/_framework/blazor.boot.json")]
    public async Task MapJobbyDashboard_ReturnedBuilder_AppliesConventionsToEveryDashboardEndpoint(string path)
    {
        await using var app = CreateAnonymousDashboardBuilder().Build();
        app.MapJobbyDashboard("/jobby").RequireHost("jobs.internal");
        await app.StartAsync(TestContext.Current.CancellationToken);
        var requiredHost = app.GetTestClient();
        requiredHost.BaseAddress = new Uri("http://jobs.internal/");
        var otherHost = app.GetTestClient();

        var allowed = await requiredHost.GetAsync(path, TestContext.Current.CancellationToken);
        var rejected = await otherHost.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);
    }

    [Fact]
    public async Task MapJobbyDashboard_OnARouteGroup_IsRejectedWithTheWayToMountIt()
    {
        await using var app = CreateAnonymousDashboardBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => app.MapGroup("/admin").MapJobbyDashboard("/jobby"));

        Assert.Contains("pass the full path as the prefix", exception.Message);
    }

    [Fact]
    public async Task DashboardEndpoints_AreNotPartOfTheApplicationApiDescription()
    {
        var builder = CreateAnonymousDashboardBuilder();
        builder.Services.AddEndpointsApiExplorer();
        await using var app = builder.Build();
        app.MapGet("/host/ping", () => "pong");
        app.MapJobbyDashboard("/jobby");
        await app.StartAsync(TestContext.Current.CancellationToken);

        var described = app.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
            .Select(api => api.RelativePath);

        // The application's own endpoint shows that descriptions are collected at all.
        Assert.Equal(["host/ping"], described);
    }

    [Fact]
    public void AddBasicAuthScheme_WithoutDecision_MapThrows()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IJobbyDashboardReadStorage>(new FakeReadStorage());
        builder.Services.AddJobbyDashboard()
            .AddBasicAuthScheme(o =>
            {
                o.Username = "admin";
                o.PasswordHash = PasswordHasher.Hash("s3cret");
            });
        var app = builder.Build();
        Assert.Throws<InvalidOperationException>(() => app.MapJobbyDashboard("/jobby"));
    }
}
