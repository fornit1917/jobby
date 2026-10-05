using Jobby.Core.Interfaces.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Jobby.Dashboard;

public static class JobbyDashboardEndpointRouteBuilderExtensions
{
    public static WebApplication MapJobbyDashboard(this WebApplication app, string prefix = "/jobby")
    {
        var state = app.Services.GetService<JobbyDashboardAuthState>()
                    ?? throw new InvalidOperationException(
                        "Jobby Dashboard services are not registered. Call builder.Services.AddJobbyDashboard(...) first.");

        using (var probeScope = app.Services.CreateScope())
        {
            if (probeScope.ServiceProvider.GetService<IJobbyDashboardReadStorage>() is null)
            {
                throw new InvalidOperationException(
                    "No IJobbyDashboardReadStorage is registered. Call e.g. AddJobbyPostgresDashboardStorage(...).");
            }
        }

        if (state.Mode == JobbyDashboardAuthMode.Unset)
        {
            throw new InvalidOperationException(
                "Jobby Dashboard is not secured. Choose one auth model: AddBasicAuth(...) (Jobby.Dashboard.Authorization), " +
                "RequireHostPolicy(...)/RequireAuthorization(...), or explicitly AllowAnonymous() for local/dev use.");
        }

        prefix = "/" + prefix.Trim().Trim('/');
        if (prefix == "/")
        {
            throw new InvalidOperationException("Jobby Dashboard prefix must be a non-root path, e.g. \"/jobby\".");
        }

        // Real endpoints preserve explicit AllowAnonymous/RequireAuthorization metadata under FallbackPolicy.
        var group = app.MapGroup(prefix);

        JobbyDashboardApi.MapAll(group, state);

        var mountPrefix = prefix;

        app.MapGet(mountPrefix, ctx =>
        {
            if ((ctx.Request.Path.Value ?? string.Empty).EndsWith('/'))
            {
                return JobbyDashboardShell.ServeAsync(ctx, mountPrefix);
            }

            var pathBase = ctx.Request.PathBase.HasValue ? ctx.Request.PathBase.Value : string.Empty;
            ctx.Response.Redirect(pathBase + mountPrefix + "/" + ctx.Request.QueryString);
            return Task.CompletedTask;
        }).AllowAnonymous();

        group.MapGet("{**path}",
                (string path, HttpContext ctx) => JobbyDashboardShell.ServeAsync(ctx, mountPrefix, path))
            .AllowAnonymous();

        return app;
    }
}