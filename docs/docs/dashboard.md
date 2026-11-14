# Dashboard

Jobby includes an optional dashboard UI for observing and managing background jobs in any ASP.NET Core host - Web API, MVC, or Blazor.

The dashboard is a **Blazor WebAssembly** single-page application served as static files from the host, backed by a **stateless JSON API** (`/jobby/api/*`). It has no SignalR circuit, no server-side component state, and no session affinity requirement.

## Packages

| Package | Purpose |
|---|---|
| `Jobby.Dashboard` | Blazor WebAssembly UI (embedded static files) + JSON API endpoints; `AddJobbyDashboard()` / `MapJobbyDashboard()` |
| `Jobby.Postgres` | `AddJobbyPostgresDashboardStorage()` - PostgreSQL read and command storage |
| `Jobby.Dashboard.Authorization.Basic` | Optional first-party Basic authentication (`AddBasicAuth` / `AddBasicAuthScheme`) and `PasswordHasher` |

`Jobby.Dashboard` targets .NET 8 and depends on ASP.NET Core 8. Applications consuming the NuGet packages do not need a WebAssembly build workload. It does not affect the Jobby engine itself: removing the package and its registration calls leaves the engine behavior unchanged.

Install `Jobby.Dashboard.Authorization.Basic` only when you want to use the built-in Basic authentication helpers. The dashboard can also run anonymously for local development, or use authentication/authorization already configured by the host application.

`Jobby.Dashboard.UI` is the WebAssembly implementation project. Its compiled assets are embedded in `Jobby.Dashboard`; consumers do not install it as a separate NuGet package.

## Installation

Install the dashboard and its ASP.NET Core/PostgreSQL dependencies in the host project:

```bash
dotnet add package Jobby.Core
dotnet add package Jobby.Postgres
dotnet add package Jobby.AspNetCore
dotnet add package Jobby.Dashboard
```

For the built-in Basic authentication helpers, also install:

```bash
dotnet add package Jobby.Dashboard.Authorization.Basic
```

Use the same release version for all Jobby packages. Host policies and inline authorization policies work without the Basic package.

## Setup

### 1. Register services (before `builder.Build()`)

`AddJobbyDashboard()` returns an `IJobbyDashboardBuilder`. You **must** make exactly one authorization decision on the returned builder before calling `MapJobbyDashboard` (see [Security](#security)).

```csharp
IJobbyDashboardBuilder dashboardBuilder = builder.Services.AddJobbyDashboard(o =>
{
    o.RefreshInterval            = TimeSpan.FromSeconds(5);
    o.StaleServerThresholdSeconds = 300;
    o.ReadOnly                   = false;
});

dashboardBuilder.AllowAnonymous();

// Schema and prefix MUST match the engine's PostgreSQL settings (see below).
builder.Services.AddJobbyPostgresDashboardStorage(npgsqlDataSource, o =>
{
    o.SchemaName   = "";          // empty string -> public schema (default)
    o.TablesPrefix = "jobby_";
    o.CountsCacheDuration = TimeSpan.FromSeconds(2);   // default; see "Job counts" below
});
```

`ReadOnly = false` allows management actions when the storage provider also registers command storage. Set it to `true` when the dashboard should only expose monitoring views.

### 2. Map the dashboard endpoint (after `app.Build()`)

```csharp
app.MapJobbyDashboard("/jobby");
```

The mount prefix must be a non-root path. The dashboard is then accessible at `/jobby`.

### Full example

This example allows anonymous access for local development. Replace `AllowAnonymous()` with a host policy or Basic authentication for a protected deployment. Configure the `Jobby` connection string and register the job handlers from the host assembly.

```csharp
using Jobby.AspNetCore;
using Jobby.Core.Interfaces;
using Jobby.Dashboard;
using Jobby.Postgres.ConfigurationExtensions;
using Jobby.Postgres.Dashboard;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Jobby")
    ?? throw new InvalidOperationException("Connection string 'Jobby' was not found.");

await using var dataSource = NpgsqlDataSource.Create(connectionString);
builder.Services.AddSingleton(dataSource);

builder.Services.AddJobbyServerAndClient(jobby =>
{
    jobby.AddJobsFromAssemblies(typeof(Program).Assembly);
    jobby.ConfigureJobby((sp, config) =>
    {
        config.UsePostgresql(sp.GetRequiredService<NpgsqlDataSource>());
    });
});

builder.Services
    .AddJobbyDashboard()
    .AllowAnonymous();

builder.Services.AddJobbyPostgresDashboardStorage(dataSource, o =>
{
    o.SchemaName   = "";
    o.TablesPrefix = "jobby_";
});

var app = builder.Build();

app.Services.GetRequiredService<IJobbyStorageMigrator>().Migrate();

app.MapJobbyDashboard("/jobby");
app.Run();
```

## Security

**The dashboard is secure by default.** `MapJobbyDashboard` throws at application startup unless exactly one authorization decision has been made on the `IJobbyDashboardBuilder`. Calling two decisions, or none, both result in a startup exception.

The static Blazor WebAssembly application and framework assets are **always public** - they contain no secrets and must be downloadable by the browser before the app boots. Only the JSON API (`/jobby/api/*`) is gated by the chosen policy. This architecture holds even when the host sets a global `AuthorizationOptions.FallbackPolicy`: the static assets carry explicit `AllowAnonymous`; the API carries `RequireAuthorization`.

> **Authentication/authorization middleware is required.** The dashboard protects its API with endpoint authorization metadata (`RequireAuthorization`); it does not insert middleware into your pipeline. With the standard minimal hosting model this works out of the box: `AddJobbyDashboard` registers the authentication/authorization services and `WebApplication` adds `UseAuthentication`/`UseAuthorization` automatically. If you build the middleware pipeline manually (for example with `UseRouting`/`UseEndpoints`), you **must** call `app.UseAuthentication()` and `app.UseAuthorization()` before the dashboard endpoints - without the authorization middleware the API's authorization metadata is not enforced.

Choose exactly one of the following:

### Option 1 - Anonymous (local dev only)

```csharp
builder.Services.AddJobbyDashboard().AllowAnonymous();
```

### Option 2 - Reuse an existing host policy

Keep the host's existing authentication registration and named authorization policies. This option requires only `Jobby.Dashboard`, without the Basic package.

```csharp
builder.Services
    .AddJobbyDashboard()
    .RequireHostPolicy("MyReadPolicy");
    // optional overload: .RequireHostPolicy("MyReadPolicy", "MyManagePolicy")
```

### Option 3 - Define the policy inline

```csharp
builder.Services
    .AddJobbyDashboard()
    .RequireAuthorization(
        read    => read.RequireRole("jobby-read"),
        manage  => manage.RequireRole("jobby-manage")
    );
```

### Option 4 - First-party Basic authentication (`Jobby.Dashboard.Authorization.Basic`)

For services without an existing auth system, the optional `Jobby.Dashboard.Authorization.Basic` package provides a single-credential Basic auth scheme backed by a PBKDF2-hashed password.

```csharp
using Jobby.Dashboard.Authorization.Basic;

builder.Services
    .AddJobbyDashboard()
    .AddBasicAuth(o =>
    {
        o.Username     = "admin";
        o.PasswordHash = builder.Configuration["JobbyDashboard:PasswordHash"]
            ?? throw new InvalidOperationException("JobbyDashboard:PasswordHash is required.");
    });

app.MapJobbyDashboard("/jobby");
```

`AddBasicAuth` registers the Basic authentication scheme **and** configures dashboard authorization in a single call - do not combine it with any other decision method.

**Generate the password hash** and store it in the host configuration as `JobbyDashboard:PasswordHash` (never store a plaintext password):

```csharp
string hash = Jobby.Dashboard.Authorization.Basic.PasswordHasher.Hash("your-password");
```

`PasswordHasher.Hash` produces a `JDPBKDF2$v1$...` string (PBKDF2-HMAC-SHA256). `AddBasicAuth` throws at startup if the supplied `PasswordHash` is not a valid PBKDF2 encoding.

> **HTTPS required:** Basic auth transmits credentials base64-encoded. Always serve the dashboard over HTTPS in production.

### Option 5 - Basic + JWT composition (advanced)

Use `AddBasicAuthScheme` to register the Basic scheme without making an auth decision, then call `RequireAuthorization` to combine it with Bearer:

```csharp
using Jobby.Dashboard.Authorization.Basic;
using Microsoft.AspNetCore.Authentication.JwtBearer;

builder.Services
    .AddJobbyDashboard()
    .AddBasicAuthScheme(o =>
    {
        o.Username     = "admin";
        o.PasswordHash = builder.Configuration["JobbyDashboard:PasswordHash"]
            ?? throw new InvalidOperationException("JobbyDashboard:PasswordHash is required.");
    })
    .RequireAuthorization(p =>
        p.AddAuthenticationSchemes(
                JobbyDashboardBasicDefaults.Scheme,
                JwtBearerDefaults.AuthenticationScheme)
         .RequireAuthenticatedUser());
```

Register the Bearer scheme in the host before using this composition.

When unauthenticated, the JSON API responds `401` with both `WWW-Authenticate: Basic` and `WWW-Authenticate: Bearer` - browsers act on `Basic`, programmatic clients on `Bearer`.

## Schema / prefix matching

The storage options (`SchemaName`, `TablesPrefix`) passed to `AddJobbyPostgresDashboardStorage` **must match** the corresponding settings passed to `UsePostgresql(...)` in the engine configuration. A mismatch causes the dashboard to query the wrong (or non-existent) tables.

```csharp
config.UsePostgresql(pg =>
{
    pg.UseDataSource(dataSource);
    pg.UseSchemaName("jobs");
    pg.UseTablesPrefix("jobby_");
});

builder.Services.AddJobbyPostgresDashboardStorage(dataSource, o =>
{
    o.SchemaName   = "jobs";
    o.TablesPrefix = "jobby_";
});
```

## Display settings

`JobbyDashboardOptions` can set the initial time zone, date/time format, and UI language. The client reads these values from `GET /jobby/api/config` when it starts.

Users can change the same settings in the dashboard top bar. Their choice is saved in `localStorage` and takes priority over the server defaults until they reset it.

```csharp
using Jobby.Core.Models.Dashboard;

builder.Services
    .AddJobbyDashboard(o =>
    {
        o.DefaultTimeZone   = TimeZoneInfo.Utc;
        o.DefaultDateStyle  = DashboardDateStyle.Iso;
        o.DefaultTimeStyle  = DashboardTimeStyle.H24WithSeconds;
        o.DefaultLanguage   = DashboardLanguage.English;
    })
    .AllowAnonymous();
```

`DefaultTimeZone` is `TimeZoneInfo.Utc` by default. Use `TimeZoneInfo.FindSystemTimeZoneById(...)` if you want another zone. The API returns timestamps in UTC. The UI converts them to the selected time zone for display, and converts date filters back to UTC before sending requests.

`/api/config` also includes `ServerTimeUtc`. The client uses it for relative values such as "5 minutes ago", so those labels do not depend on the browser clock.

## Features and API

The dashboard refreshes data by polling at the configured `RefreshInterval`. It displays:

- **Overview** - live job counts by status and queue.
- **Jobs** - paginated, filterable, sortable job list.
- **Job detail** - full job record including parameters, error detail, and timestamps.
- **Recurrent jobs** - scheduled job definitions.
- **Servers** - connected Jobby server instances and heartbeat health.
- **Queues** - per-queue status breakdown.
- **Locked groups** - serializable execution groups blocked by failed or lost jobs.

Management actions are enabled when an `IJobbyDashboardCommandStorage` is registered and `JobbyDashboardOptions.ReadOnly` is `false`. `AddJobbyPostgresDashboardStorage()` registers both read and command storage. If management is disabled, the UI hides management actions and the corresponding API routes return `404`. The top-bar Read only mode hides controls for convenience; enforce read-only access on the server with `JobbyDashboardOptions.ReadOnly` or authorization policies.

### Scheduled dates and ordering

The Jobs From/To filters select the scheduled start date in the dashboard's selected time zone. Both calendar days are included; the UI converts their boundaries to UTC, including daylight-saving transitions. A reversed range displays an error and retains the entire last valid result until corrected. Changes to status, queue, or search filters made while the range is invalid apply once the range is corrected. Creation time is visible and sortable.

Jobs and a queue's All jobs view initially sort by scheduled start ascending, so the earliest scheduled jobs appear first. The queue backlog uses the same order. Users can select another sort column and direction.

The jobs API accepts inclusive UTC timestamp bounds `scheduledFrom` and `scheduledTo`; the corresponding `DashboardJobsQuery` properties are `ScheduledFrom` and `ScheduledTo`. The date range refers to the current scheduled start, including retries and rescheduling, rather than creation time or actual execution time.

### Job counts

Counting jobs per queue and status reads the whole jobs table, so one application instance runs that query once and reuses the result for `CountsCacheDuration` (2 seconds by default) for every open dashboard. The Overview statistics and queue cards always use these shared counts. The total of a job list filtered only by queue and status reuses them while they are loaded and is counted directly otherwise; totals for name search and scheduled-date filters are always counted directly. Job rows, job details, recurrent jobs, locked groups and management actions are never cached.

A count can therefore be up to `CountsCacheDuration` old. Retrying or deleting a job from the dashboard discards the counts of the instance that handled the request. Keep the duration shorter than `RefreshInterval`, otherwise a dashboard can receive the same counts on consecutive refreshes. `TimeSpan.Zero` shares the counts only between requests that arrive while the query is running.

By default, job lists are read through the existing `(queue_name, status, scheduled_start_at)` index. Sorting by another column, name search, scheduled-date filters and very deep pages scan the table instead and are slower on large tables.

### Run scheduled jobs early

Trigger is available only for jobs in `Scheduled` status, including recurring jobs. It advances a future scheduled start to the current time and leaves an already-due timestamp unchanged. A worker executes the job when it can acquire it under the normal queue, server, and serializable-group rules; the HTTP request does not run the handler itself.

For recurring jobs, Trigger uses the same persisted job and ID, without creating a one-off copy. The normal scheduler calculates the next run after execution, so an interval schedule may shift. For example, an hourly job due at 12:00 that is triggered at 11:10 and completes at 11:12 can next run at 12:10 or 12:12, depending on `CalculateNextFromPrev`. A cron schedule uses its next matching time. Repeated trigger requests while the invocation is pending do not queue additional copies; triggering after completion can advance the next invocation.

### Exclusive recurring definitions

The recurring list and job details show `IsExclusive` (`is_exclusive` in storage). An exclusive recurring definition is unique among exclusive definitions with the same job name. Non-exclusive definitions with that name can coexist. This flag controls definition uniqueness, not mutual exclusion of executions; use serializable groups when jobs must execute sequentially.

### Read API

Read endpoints are exposed under the mount prefix:

| Endpoint | Description |
|---|---|
| `GET /jobby/api/config` | Dashboard configuration fetched by the WASM client at startup |
| `GET /jobby/api/stats` | Aggregate job statistics |
| `GET /jobby/api/jobs` | Paginated job list (query params: `status`, `statuses`, `queueName`, `jobNameSearch`, `scheduledFrom`, `scheduledTo`, `page`, `pageSize`, `sortBy`, `sortDescending`) |
| `GET /jobby/api/jobs/{id}` | Single job detail |
| `GET /jobby/api/recurrent` | Paginated recurrent job definitions |
| `GET /jobby/api/servers` | Registered Jobby server instances |
| `GET /jobby/api/queues` | Per-queue statistics |
| `GET /jobby/api/locked-groups` | Paginated locked execution groups |
| `GET /jobby/api/locked-groups/count` | Number of locked execution groups |
| `GET /jobby/api/locked-groups/detail?groupId={id}` | Locked group details and affected jobs |

### Management API

Management endpoints validate the antiforgery header and token returned by `/jobby/api/config`:

| Endpoint | Description |
|---|---|
| `POST /jobby/api/jobs/{id}/trigger` | Request early execution of a scheduled job, including recurring jobs |
| `POST /jobby/api/jobs/{id}/retry` | Retry a failed or frozen non-recurrent job |
| `DELETE /jobby/api/jobs/{id}` | Delete a non-running, non-recurrent job |
| `DELETE /jobby/api/recurrent/{id}` | Delete a recurrent job definition |
| `POST /jobby/api/locked-groups/unlock-request` | Request unlock and unfreeze for a locked execution group |

Unknown paths under `/jobby/api/*` return `404`.

## Production hosting - Kubernetes / multi-replica

The dashboard is a **stateless** Blazor WebAssembly application backed by a **stateless** JSON API. Because it is served as static WebAssembly files rather than an interactive server-side Blazor app:

- **No SignalR circuit.** There is no WebSocket connection to maintain.
- **No sticky sessions / session affinity.** Requests can be freely load-balanced across replicas.
- **No server-side component state.** The dashboard does not keep UI state in the ASP.NET Core process.

Normal production concerns still apply:

- **HTTPS** - required when using Basic authentication (credentials are base64-encoded, not encrypted).
- **Reverse proxy / sub-path hosting** - configure `UseForwardedHeaders` and the proxy path base as usual. The dashboard injects the correct `<base href>` at runtime, so sub-path hosting works without manual configuration.
- **Consistent data** - all reads come from the shared PostgreSQL database, so every replica returns the same cluster-wide view. The only in-memory data is each replica's copy of the [job counts](#job-counts), reused for `CountsCacheDuration`: replicas can report counts that differ by the changes of the last few seconds, and a retry or delete refreshes the counts only on the replica that handled it. Job rows and management actions always go to the database.
- **Antiforgery / Data Protection** - management requests use ASP.NET Core antiforgery. If management actions are enabled and requests can be routed to different replicas, share ASP.NET Core Data Protection keys across replicas as you would for any antiforgery-enabled ASP.NET Core app.

## What Next

- [Install and Config](./install-and-config)
- [Fault Tolerance](./fault-tolerance)
- [Observability](./observability)
