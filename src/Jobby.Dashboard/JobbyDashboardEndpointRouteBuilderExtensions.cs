using Jobby.Core.Interfaces.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Jobby.Dashboard;

public static class JobbyDashboardEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Adds the dashboard at <paramref name="prefix"/>, a path from the root of the application.
    /// </summary>
    /// <returns>A builder for conventions that apply to every endpoint of the dashboard.</returns>
    public static IEndpointConventionBuilder MapJobbyDashboard(
        this IEndpointRouteBuilder endpoints, string prefix = "/jobby")
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // The page is told its address through the prefix, and a route group would silently move the endpoints.
        if (endpoints is RouteGroupBuilder)
        {
            throw new InvalidOperationException(
                "Jobby Dashboard cannot be mapped inside a route group. " +
                "Map it on the application and pass the full path as the prefix, e.g. \"/admin/jobby\".");
        }

        var services = endpoints.ServiceProvider;
        var state = services.GetService<JobbyDashboardAuthState>()
                    ?? throw new InvalidOperationException(
                        "Jobby Dashboard services are not registered. Call builder.Services.AddJobbyDashboard(...) first.");

        using (var probeScope = services.CreateScope())
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
                "Jobby Dashboard is not secured. Choose one auth model: AddBasicAuth(...) (Jobby.Dashboard.Authorization.Basic), " +
                "RequireHostPolicy(...)/RequireAuthorization(...), or explicitly AllowAnonymous() for local/dev use.");
        }

        prefix = "/" + prefix.Trim().Trim('/');
        if (prefix == "/")
        {
            throw new InvalidOperationException("Jobby Dashboard prefix must be a non-root path, e.g. \"/jobby\".");
        }

        // Real endpoints preserve explicit AllowAnonymous/RequireAuthorization metadata under FallbackPolicy.
        // They serve the dashboard's own page, so they stay out of the application's API description (OpenAPI).
        var group = endpoints.MapGroup(prefix).ExcludeFromDescription();

        JobbyDashboardApi.MapAll(group, state);

        var mountPrefix = prefix;

        group.MapGet("/", ctx =>
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

        return group;
    }
}
