using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Jobby.Dashboard;

internal static class JobbyDashboardApi
{
    public static void MapAll(IEndpointRouteBuilder endpoints, JobbyDashboardAuthState state)
    {
        var api = endpoints.MapGroup("api");
        if (state.Mode != JobbyDashboardAuthMode.Anonymous)
        {
            api.RequireAuthorization(state.ReadPolicy);
        }
        else
        {
            api.AllowAnonymous();
        }

        api.MapGet("config",
            (HttpContext http, IDashboardDataReader reader, JobbyDashboardOptions opts, IAntiforgery antiforgery) =>
            {
                var tokens = antiforgery.GetAndStoreTokens(http);
                http.Response.Headers.CacheControl = "no-store";
                http.Response.Headers.Pragma = "no-cache";

                if (string.IsNullOrWhiteSpace(tokens.HeaderName) || string.IsNullOrWhiteSpace(tokens.RequestToken))
                {
                    return Results.Problem(
                        "Jobby Dashboard requires ASP.NET Core antiforgery to be configured with a request-token header.",
                        statusCode: StatusCodes.Status500InternalServerError);
                }

                var defaultTimeZoneId = opts.DefaultTimeZone.Id;
                if (!TimeZoneInfo.TryFindSystemTimeZoneById(defaultTimeZoneId, out _))
                {
                    defaultTimeZoneId = null;
                }

                return Results.Ok(new JobbyDashboardClientConfigDto
                {
                    RefreshIntervalSeconds = opts.RefreshInterval?.TotalSeconds,
                    StaleServerThresholdSeconds = opts.StaleServerThresholdSeconds,
                    ReadOnly = opts.ReadOnly,
                    ManagementEnabled = reader.ManagementEnabled,
                    ManagementRequestHeaderName = tokens.HeaderName,
                    ManagementRequestToken = tokens.RequestToken,
                    DefaultTimeZoneId = defaultTimeZoneId,
                    DefaultDateStyle = opts.DefaultDateStyle,
                    DefaultTimeStyle = opts.DefaultTimeStyle,
                    ServerTimeUtc = DateTime.UtcNow,
                    DefaultLanguage = opts.DefaultLanguage,
                });
            });

        api.MapGet("stats",
            (IDashboardDataReader reader, CancellationToken cancellationToken) =>
                reader.GetStatsAsync(cancellationToken));

        api.MapGet("jobs", (HttpContext http, IDashboardDataReader reader, CancellationToken cancellationToken) =>
            reader.GetJobsAsync(ParseJobsQuery(http.Request.Query), cancellationToken));

        api.MapGet("jobs/{id:guid}",
            async (Guid id, IDashboardDataReader reader, CancellationToken cancellationToken) =>
                await reader.GetJobByIdAsync(id, cancellationToken) is { } details
                    ? Results.Ok(details)
                    : Results.NotFound());

        api.MapGet("recurrent", (HttpContext http, IDashboardDataReader reader, CancellationToken cancellationToken) =>
            reader.GetRecurrentJobsAsync(ParseRecurrentQuery(http.Request.Query), cancellationToken));

        api.MapGet("servers",
            (IDashboardDataReader reader, CancellationToken cancellationToken) =>
                reader.GetServersAsync(cancellationToken));
        api.MapGet("queues",
            (IDashboardDataReader reader, CancellationToken cancellationToken) =>
                reader.GetQueuesAsync(cancellationToken));

        api.MapGet("locked-groups",
            (HttpContext http, IDashboardDataReader reader, CancellationToken cancellationToken) =>
                reader.GetLockedGroupsAsync(ParseLockedGroupsQuery(http.Request.Query), cancellationToken));

        api.MapGet("locked-groups/detail",
            async (HttpContext http, IDashboardDataReader reader, CancellationToken cancellationToken) =>
            {
                var groupId = QueryValue(http.Request.Query, "groupId");
                if (string.IsNullOrEmpty(groupId))
                {
                    return Results.NotFound();
                }

                return await reader.GetLockedGroupByIdAsync(groupId, cancellationToken) is { } details
                    ? Results.Ok(details)
                    : Results.NotFound();
            });

        MapManagementActions(api, state);
    }

    private static void MapManagementActions(IEndpointRouteBuilder api, JobbyDashboardAuthState state)
    {
        Manage(api.MapPost("jobs/{id:guid}/trigger",
            (Guid id, HttpContext http, IDashboardDataReader reader, IAntiforgery antiforgery,
                    CancellationToken cancellationToken) =>
                RunAsync(http, reader, antiforgery, () => reader.TriggerJobsAsync([id], cancellationToken))), state);

        Manage(api.MapPost("jobs/{id:guid}/retry",
            (Guid id, HttpContext http, IDashboardDataReader reader, IAntiforgery antiforgery,
                    CancellationToken cancellationToken) =>
                RunAsync(http, reader, antiforgery, () => reader.RetryJobsAsync([id], cancellationToken))), state);

        Manage(api.MapDelete("jobs/{id:guid}",
            (Guid id, HttpContext http, IDashboardDataReader reader, IAntiforgery antiforgery,
                    CancellationToken cancellationToken) =>
                RunAsync(http, reader, antiforgery, () => reader.DeleteJobsAsync([id], cancellationToken))), state);

        Manage(api.MapDelete("recurrent/{id:guid}",
                (Guid id, HttpContext http, IDashboardDataReader reader, IAntiforgery antiforgery,
                        CancellationToken cancellationToken) =>
                    RunAsync(http, reader, antiforgery,
                        () => reader.DeleteRecurrentJobsAsync([id], cancellationToken))),
            state);

        Manage(api.MapPost("locked-groups/unlock-request",
            async (UnlockGroupRequest body, HttpContext http, IDashboardDataReader reader,
                IAntiforgery antiforgery, CancellationToken cancellationToken) =>
            {
                if (!reader.ManagementEnabled)
                {
                    return Results.NotFound();
                }

                try
                {
                    await antiforgery.ValidateRequestAsync(http);
                }
                catch (AntiforgeryValidationException)
                {
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }

                if (string.IsNullOrWhiteSpace(body.GroupId))
                {
                    return Results.BadRequest();
                }

                var affected = await reader.RequestGroupUnlockAsync(body.GroupId, cancellationToken);
                return Results.Ok(new RequestGroupUnlockResultDto(affected > 0));
            }), state);
    }

    [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
    private static RouteHandlerBuilder Manage(RouteHandlerBuilder builder, JobbyDashboardAuthState state) =>
        state.Mode == JobbyDashboardAuthMode.Anonymous ? builder : builder.RequireAuthorization(state.ManagePolicy);

    private static async Task<IResult> RunAsync(HttpContext http, IDashboardDataReader reader,
        IAntiforgery antiforgery, Func<Task<int>> action)
    {
        if (!reader.ManagementEnabled)
        {
            return Results.NotFound();
        }

        try
        {
            await antiforgery.ValidateRequestAsync(http);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return await action() > 0 ? Results.NoContent() : Results.NotFound();
    }

    private static DashboardJobsQuery ParseJobsQuery(IQueryCollection query)
    {
        return new DashboardJobsQuery
        {
            Status = Enum.TryParse<JobStatus>(QueryValue(query, "status"), true, out var s) ? s : null,
            Statuses = ParseStatuses(QueryValue(query, "statuses")),
            QueueName = QueryValue(query, "queueName"),
            JobNameSearch = QueryValue(query, "jobNameSearch"),
            // Note: AssumeUniversal | AdjustToUniversal: values are bound to timestamptz parameters, which Npgsql only accepts with DateTimeKind.Utc.
            CreatedFrom = DateTime.TryParse(QueryValue(query, "createdFrom"), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var cf)
                ? cf
                : null,
            CreatedTo = DateTime.TryParse(QueryValue(query, "createdTo"), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var ctv)
                ? ctv
                : null,
            Page = int.TryParse(QueryValue(query, "page"), out var p) ? p : 1,
            PageSize = int.TryParse(QueryValue(query, "pageSize"), out var ps) ? ps : 50,
            SortBy = Enum.TryParse<DashboardJobsSortBy>(QueryValue(query, "sortBy"), true, out var sb)
                ? sb
                : DashboardJobsSortBy.CreatedAt,
            SortDescending = !bool.TryParse(QueryValue(query, "sortDescending"), out var sd) || sd,
        };
    }

    private static DashboardRecurrentJobsQuery ParseRecurrentQuery(IQueryCollection query)
    {
        return new DashboardRecurrentJobsQuery
        {
            JobNameSearch = QueryValue(query, "jobNameSearch"),
            Page = int.TryParse(QueryValue(query, "page"), out var p) ? p : 1,
            PageSize = int.TryParse(QueryValue(query, "pageSize"), out var ps) ? ps : 50,
        };
    }

    private static LockedGroupsQuery ParseLockedGroupsQuery(IQueryCollection query)
    {
        return new LockedGroupsQuery
        {
            Page = int.TryParse(QueryValue(query, "page"), out var p) ? p : 1,
            PageSize = int.TryParse(QueryValue(query, "pageSize"), out var ps) ? ps : 50,
        };
    }

    private static string? QueryValue(IQueryCollection query, string key)
    {
        var value = query[key].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static IReadOnlyList<JobStatus>? ParseStatuses(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var result = new List<JobStatus>();
        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<JobStatus>(token, ignoreCase: true, out var status) && Enum.IsDefined(status))
            {
                result.Add(status);
            }
        }

        return result.Count > 0 ? result : null;
    }
}
