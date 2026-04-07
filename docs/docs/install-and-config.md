# Install and Config

Use this page after [Quick Start](./quickstart). It shows the supported setup modes and the few configuration options most projects need first.

## Choose Your Setup Mode

- ASP.NET Core server + client: use this when the app both enqueues and processes jobs.
- ASP.NET Core client only: use this when the app only creates jobs and another service processes them.
- Non-ASP.NET Core: use this for console apps, workers, or custom hosts where you manage Jobby yourself.

## ASP.NET Core: Server + Client

This is the standard setup for an application that both creates and executes jobs.

```csharp
using Jobby.AspNetCore;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Postgres.ConfigurationExtensions;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Jobby")
    ?? throw new InvalidOperationException("Connection string 'Jobby' was not found.");

builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));

builder.Services.AddJobbyServerAndClient(jobby =>
{
    jobby.AddJobsFromAssemblies(typeof(Program).Assembly);

    jobby.ConfigureJobby((sp, config) =>
    {
        config
            .UsePostgresql(sp.GetRequiredService<NpgsqlDataSource>())
            .UseServerSettings(new JobbyServerSettings
            {
                MaxDegreeOfParallelism = 10,
                TakeToProcessingBatchSize = 10
            })
            .UseDefaultRetryPolicy(new RetryPolicy
            {
                MaxCount = 3,
                IntervalsSeconds = [1, 2]
            });
    });
});

var app = builder.Build();

app.Services.GetRequiredService<IJobbyStorageMigrator>().Migrate();

app.Run();
```

`AddJobbyServerAndClient` registers both `IJobbyClient` and the background processing server. Run `Migrate()` before normal traffic so Jobby can create or update its tables.

## ASP.NET Core: Client Only

Use this mode when the service only enqueues jobs.

```csharp
using Jobby.AspNetCore;
using Jobby.Postgres.ConfigurationExtensions;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Jobby")
    ?? throw new InvalidOperationException("Connection string 'Jobby' was not found.");

builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));

builder.Services.AddJobbyClient(jobby =>
{
    jobby.ConfigureJobby((sp, config) =>
    {
        config.UsePostgresql(sp.GetRequiredService<NpgsqlDataSource>());
    });
});
```

This registers `IJobbyClient`, but it does not start background job processing. Use it when another app is responsible for executing jobs.

## Non-ASP.NET Core

For console apps or custom hosts, configure Jobby directly with `JobbyBuilder`.

```csharp
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.Postgres.ConfigurationExtensions;
using Npgsql;

using var dataSource = NpgsqlDataSource.Create(connectionString);

var builder = new JobbyBuilder();

builder.AddJobsFromAssemblies(typeof(Program).Assembly);
builder
    .UsePostgresql(pg =>
    {
        pg.UseDataSource(dataSource);
    })
    .UseExecutionScopeFactory(scopeFactory)
    .UseServerSettings(new JobbyServerSettings
    {
        MaxDegreeOfParallelism = 10,
        TakeToProcessingBatchSize = 10
    });

builder.CreateStorageMigrator().Migrate();

var jobbyClient = builder.CreateJobbyClient();
var jobbyServer = builder.CreateJobbyServer();

jobbyServer.StartBackgroundService();
```

In this mode you own the setup lifecycle yourself: provide an execution scope factory, run migrations, create the client and server, and start or stop the background service explicitly.

## Common Settings

Most applications only need to adjust a few things.

### Connection String and Data Source

Use a standard ASP.NET Core connection string such as `ConnectionStrings:Jobby`, then create one shared `NpgsqlDataSource`.

### PostgreSQL Schema and Table Prefix

If Jobby shares a database with other data, you can isolate its objects with a custom schema or table prefix:

```csharp
config.UsePostgresql(pg =>
{
    pg.UseDataSource(sp.GetRequiredService<NpgsqlDataSource>());
    pg.UseSchemaName("jobs");
    pg.UseTablesPrefix("jobby_");
});
```

Use this when you want Jobby tables to be easier to identify or separated from the default schema.

### Server Settings

`JobbyServerSettings` controls how aggressively jobs are pulled and executed.

- `MaxDegreeOfParallelism`: how many jobs can run at the same time. Increase it for more throughput; lower it if your jobs are heavy or depend on limited resources.
- `TakeToProcessingBatchSize`: how many jobs Jobby reads from the queue at once. Larger values can improve throughput; smaller values reduce burstiness.
- `PollingIntervalMs`: how often the server checks for new work. Lower values reduce latency but increase database polling.

### Retry Policy

`RetryPolicy` controls what happens after a failed execution.

- `MaxCount`: maximum number of execution attempts.
- `IntervalsSeconds`: delays between retry attempts.

Start with a small retry count and short intervals, then adjust based on how long your transient failures usually last.

## What Next

- [Jobs definition](./jobs-definition)
- [Enqueue jobs](./jobs-enqueue)
- [Scheduled jobs](./jobs-schedule)
- [Retry policies](./retry-policies)
- [Multi-queues](./multiqueues)
- [Observability](./observability)
- [Middlewares](./middlewares)
- [Fault tolerance](./fault-tolerance)
