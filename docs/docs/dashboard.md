# Dashboard

Jobby has an optional web dashboard for watching and managing background jobs. You add it to an ASP.NET Core application, and it opens in the browser at a path of your choice, for example `/jobby`.

With the dashboard you can:

- see how many jobs are in each status, how full the queues are and which servers are alive;
- browse, filter and sort jobs, and open the parameters and the error of any job;
- view recurrent jobs with their schedules and next run time;
- start a scheduled job right away, retry a failed job, cancel or delete jobs;
- create jobs and recurrent jobs;
- find sequential execution groups blocked by a failed job and unlock them.

The interface is available in English, Russian and Brazilian Portuguese, with light and dark themes.

## Installation

Add the dashboard package to the application that will show the dashboard:

```bash
dotnet add package Jobby.Dashboard
```

The dashboard reads jobs from the Jobby storage, so the application also needs the `Jobby.Postgres` package. Use the same version for all Jobby packages.

If you want to protect the dashboard with the built-in login and password check, also install:

```bash
dotnet add package Jobby.Dashboard.Authorization.Basic
```

## Setup

Three calls are required: add the dashboard and decide who may open it, point it at the job storage, and choose its address.

```csharp
using Jobby.Dashboard;
using Jobby.Postgres.Dashboard;

// 1. Add the dashboard and decide who may open it (see "Access control")
builder.Services
    .AddJobbyDashboard()
    .AllowAnonymous();

// 2. Point the dashboard at the storage with the jobs
builder.Services.AddJobbyPostgresDashboardStorage(dataSource);

var app = builder.Build();

// 3. Choose the address of the dashboard
app.MapJobbyDashboard("/jobby");
```

The dashboard is now available at `/jobby`. Any path except the site root (`/`) can be used.

If Jobby is configured with a custom schema or table prefix, pass the same values to the dashboard storage, otherwise the dashboard will look for the jobs in the wrong tables:

```csharp
// Jobby configuration
config.UsePostgresql(pg =>
{
    pg.UseDataSource(dataSource);
    pg.UseSchemaName("jobs");
    pg.UseTablesPrefix("jobby_");
});

// Dashboard storage: the same schema and prefix
builder.Services.AddJobbyPostgresDashboardStorage(dataSource, o =>
{
    o.SchemaName   = "jobs";
    o.TablesPrefix = "jobby_";
});
```

### Full example

An application that runs Jobby and shows the dashboard without authentication, which is suitable for local development only:

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

builder.Services.AddJobbyPostgresDashboardStorage(dataSource);

var app = builder.Build();

app.Services.GetRequiredService<IJobbyStorageMigrator>().Migrate();

app.MapJobbyDashboard("/jobby");
app.Run();
```

## Access control

The dashboard shows job parameters and can change jobs, so you have to state explicitly who may use it. Choose exactly one of the options below when adding the dashboard. If no option is chosen, or more than one, the application fails at startup.

There are two levels of access:

- **read** - open the dashboard and view everything in it;
- **manage** - also perform actions: trigger, retry, cancel, delete and create jobs, unlock groups.

When only one rule is given, it is used for both levels.

### Anonymous access

Everyone who can reach the address can view and manage jobs. Use it only for local development.

```csharp
builder.Services.AddJobbyDashboard().AllowAnonymous();
```

### Authorization policy of the application

If the application already has authentication and named authorization policies, reuse them. The second policy is optional and applies to management actions.

```csharp
builder.Services
    .AddJobbyDashboard()
    .RequireHostPolicy("JobsRead", "JobsManage");
```

### Policy defined in place

The requirements can also be written next to the dashboard, without registering named policies:

```csharp
builder.Services
    .AddJobbyDashboard()
    .RequireAuthorization(
        read   => read.RequireRole("jobby-read"),
        manage => manage.RequireRole("jobby-manage"));
```

### Login and password

For an application without its own authentication, the `Jobby.Dashboard.Authorization.Basic` package adds a login and password check (HTTP Basic authentication). The browser asks for them when the dashboard is opened.

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
```

The password is never stored as plain text. Generate its hash once and put the hash into the application configuration:

```csharp
string hash = Jobby.Dashboard.Authorization.Basic.PasswordHasher.Hash("your-password");
```

Basic authentication sends the password with every request, so serve the dashboard over HTTPS.

### Login and password together with another scheme

To accept the dashboard login and password and, for example, JWT tokens of the application, register the Basic scheme with `AddBasicAuthScheme` and list both schemes in the policy:

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
    .RequireAuthorization(p => p
        .AddAuthenticationSchemes(
            JobbyDashboardBasicDefaults.Scheme,
            JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser());
```

### Good to know

- The access rules protect the data and the actions. The page itself can be downloaded without authentication, but it shows nothing until the rules are satisfied.
- In an application created with `WebApplication.CreateBuilder` everything works out of the box. If you build the request pipeline manually, call `app.UseAuthentication()` and `app.UseAuthorization()` before the dashboard is mapped, otherwise the access rules are not applied.

## Settings

`AddJobbyDashboard` accepts the settings of the dashboard:

```csharp
using Jobby.Core.Models.Dashboard;

builder.Services
    .AddJobbyDashboard(o =>
    {
        o.RefreshInterval = TimeSpan.FromSeconds(10);
        o.ReadOnly        = true;
        o.DefaultLanguage = DashboardLanguage.Russian;
    })
    .RequireHostPolicy("JobsRead");
```

| Setting | Default | Description |
|---|---|---|
| `RefreshInterval` | 5 seconds | How often open pages reload their data. `null` turns automatic refresh off; the **Refresh now** button still works. |
| `StaleServerThresholdSeconds` | 300 | A server is shown as stale when it has not sent a heartbeat for this number of seconds. |
| `ReadOnly` | `false` | When `true`, the dashboard only displays data: all actions are hidden and rejected. |
| `DefaultTimeZone` | `null` | Time zone of the displayed timestamps. `null` means the time zone of the user's browser. |
| `DefaultDateStyle` | `Iso` | Date format: `Iso` (2026-11-24), `DayFirstSlash` (24/11/2026), `DayFirstDot` (24.11.2026), `MonthFirstSlash` (11/24/2026). |
| `DefaultTimeStyle` | `H24WithSeconds` | Time format: `H24`, `H24WithSeconds`, `H12`, `H12WithSeconds`. |
| `DefaultLanguage` | `English` | Interface language: `English`, `Russian`, `PortugueseBrazil`. |

The time zone, the date and time formats and the language are only defaults. Every user can change them in the top bar of the dashboard; the choice is remembered in the browser.

Every timestamp is shown together with its UTC offset, for example `2026-11-24 12:10:16 UTC+2`, so it is always clear which time zone it is in.

## Working with the dashboard

### Overview

The start page summarizes the current state:

- the number of jobs in each status; a click opens the list of these jobs;
- **Queues** - for every queue, its backlog (jobs that are not finished yet) and the total number of jobs;
- **Server health** - Jobby servers and the time of their last heartbeat;
- **Recurrent jobs** - how many recurrent jobs are waiting for their next run and how many are running now;
- **Needs attention** - failed and frozen jobs.

### Recurrent

The list of recurrent jobs (see [Scheduled Jobs](./jobs-schedule)) with their schedule in a readable form, queue, status, next run and last finish time. A recurrent job can be started ahead of its schedule or deleted, and new recurrent jobs can be created.

### Jobs

The list of ordinary (non-recurrent) jobs. It can be filtered by status, queue, job name and scheduled date, and sorted by name, status, scheduled time or creation time. A click on a job opens its details: identifier, timestamps, number of attempts, server, parameters and error text.

### Queues

For every queue the page shows the number of jobs in each status, the backlog and the total. A queue can be expanded to preview its backlog, and opened to see either the backlog or all of its jobs.

### Locked groups

When a job of a [sequential execution group](./sequential-execution#group-locking-on-error) fails for good, the group can become locked and its remaining jobs are frozen. The page lists such groups with the job that locks them. **Request Unlock** removes the locking job, after which the frozen jobs of the group are scheduled to run again.

### Actions

Actions are available to users with the manage level of access when `ReadOnly` is off. A user can also switch their own dashboard to the read-only view with the toggle in the top bar, to avoid accidental clicks.

| Action | Available for | Result |
|---|---|---|
| Trigger now | Scheduled jobs, including recurrent ones | The job is started as soon as a server can take it, without waiting for its scheduled time. |
| Retry | Failed jobs | The job is scheduled again with its attempt counter reset. |
| Cancel | Scheduled and waiting jobs | The job is deleted before it has started. |
| Delete | Completed, failed and frozen jobs; recurrent jobs | The job is deleted. A deleted recurrent job stops running. |
| Create job, Create recurrent job | - | A new job is added, see below. |
| Request Unlock | Locked groups | The group is unlocked and its frozen jobs are scheduled to run again. |

A frozen job cannot be retried on its own, because its group is still locked: request the unlock of the group instead (**Request Unlock**). The job that locks a group cannot be retried or deleted either.

Triggering a recurrent job does not create a copy of it: the job simply runs earlier, and its next run is then calculated by its schedule as usual.

### Creating jobs

The **Create job** button on the Jobs page and the **Create recurrent job** button on the Recurrent page open a form with the following fields:

- **Job** - one of the jobs registered in the application;
- **Parameters** - the job command as JSON, for example `{"Email": "user@example.com"}`. The JSON is parsed by the same serializer that Jobby uses when the job runs, and the form shows an error if it cannot be parsed. With the default settings property names are case-sensitive and unknown properties are ignored, so check the names carefully;
- **Queue** - optional; when empty, the job goes to the queue it uses by default;
- **Cron expression** - for a recurrent job;
- **Exclusive** - for a recurrent job. With the box checked the job is created as exclusive: if an exclusive recurrent job with the same name already exists, it is updated instead of a new one being created. Clear the box to add one more, independent schedule. See [Exclusivity](./jobs-schedule#exclusivity).

A job created in the dashboard is the same as a job created from code with `IJobbyClient`.

Jobs can be created only when the application that shows the dashboard also registers Jobby (`AddJobbyServerAndClient` or `AddJobbyClient`) together with its jobs (`AddJob` or `AddJobsFromAssemblies`). Otherwise the buttons are not shown.

## Large job tables

Counting jobs by queue and status requires reading the whole jobs table. To keep the load on the database low when many people have the dashboard open, the counts are cached for a short time. The time is set with `CountsCacheDuration` of the dashboard storage (2 seconds by default):

```csharp
builder.Services.AddJobbyPostgresDashboardStorage(dataSource, o =>
{
    o.CountsCacheDuration = TimeSpan.FromSeconds(4);
});
```

The numbers on the Overview and Queues pages can therefore lag behind by this time. Keep the value smaller than `RefreshInterval`. Job lists and job details are always read directly from the storage.

On very large tables the Jobs page works fastest with its default order (by scheduled time). Sorting by another column, searching by name and filtering by date are slower.

## Running several instances

The dashboard does not keep user sessions on the server, so the application can run in any number of instances behind a load balancer without sticky sessions.

One thing has to be configured when actions are enabled: the instances must share their [ASP.NET Core Data Protection keys](https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview), as for any ASP.NET Core application with forms. Otherwise an action sent to another instance is rejected.

## What Next

- [Install and Config](./install-and-config)
- [Fault Tolerance](./fault-tolerance)
- [Observability](./observability)
