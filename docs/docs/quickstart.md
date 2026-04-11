# Quick Start

Get Jobby running in a minimal ASP.NET Core app: install packages, define one job, start the server, and enqueue a test job.

## Prerequisites

- .NET 8 or later
- PostgreSQL
- A new or existing ASP.NET Core app

## 1. Install Packages

```shell
dotnet add package Jobby.Core
dotnet add package Jobby.Postgres
dotnet add package Jobby.AspNetCore
```

## 2. Create a Job

Create a command and a handler. Jobby executes handlers in the background.

```csharp
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Microsoft.Extensions.Logging;

public sealed class HelloJob : IJobCommand
{
    public string Message { get; init; } = "Hello from Jobby";

    public static string GetJobName() => "hello-job";
}

public sealed class HelloJobHandler : IJobCommandHandler<HelloJob>
{
    private readonly ILogger<HelloJobHandler> _logger;

    public HelloJobHandler(ILogger<HelloJobHandler> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync(HelloJob command, JobExecutionContext ctx)
    {
        _logger.LogInformation(
            "Job executed. Message: {Message}. Attempt: {Attempt}",
            command.Message,
            ctx.StartedCount);

        return Task.CompletedTask;
    }
}
```

## 3. Configure Jobby

Add Jobby to `Program.cs`.

`ConnectionStrings__Jobby` is a standard ASP.NET Core connection string key. Example value:
`Host=localhost;Username=postgres;Password=postgres;Database=jobby_quickstart`

```csharp
using Jobby.AspNetCore;
using Jobby.Core.Interfaces;
using Jobby.Postgres.ConfigurationExtensions;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Jobby")
    ?? throw new InvalidOperationException("Connection string 'Jobby' was not found.");

builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));

builder.Services.AddJobbyServerAndClient(jobby =>
{
    jobby.AddJobsFromAssemblies(typeof(HelloJob).Assembly);

    jobby.ConfigureJobby((sp, config) =>
    {
        config.UsePostgresql(sp.GetRequiredService<NpgsqlDataSource>());
    });
});

var app = builder.Build();

// Create or update Jobby tables
app.Services.GetRequiredService<IJobbyStorageMigrator>().Migrate();

// Enqueue one test job on startup
var jobbyClient = app.Services.GetRequiredService<IJobbyClient>();
await jobbyClient.EnqueueCommandAsync(new HelloJob
{
    Message = "Jobby is working"
});

app.MapGet("/", () => "Jobby quick start is running");

app.Run();
```

## 4. Run the App

Set the connection string and start the app:

```shell
ConnectionStrings__Jobby="Host=localhost;Username=postgres;Password=postgres;Database=jobby_quickstart" dotnet run
```

If you use `appsettings.json`, put it under:

```json
{
  "ConnectionStrings": {
    "Jobby": "Host=localhost;Username=postgres;Password=postgres;Database=jobby_quickstart"
  }
}
```

## 5. What You Should See

On startup Jobby will:

- create its tables if they do not exist
- start the background processing service
- enqueue `HelloJob`
- execute it almost immediately

You should see a log message similar to:

```text
Job executed. Message: Jobby is working. Attempt: 1
```

## Next

- [Install and Config](./install-and-config)
- [Jobs definition](./jobs-definition)
- [Enqueue jobs](./jobs-enqueue)
- [Scheduled jobs](./jobs-schedule)
