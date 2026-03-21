# Быстрый старт

Минимальный запуск Jobby в ASP.NET Core: установить пакеты, описать одну задачу, поднять сервер и поставить тестовую задачу в очередь.

## Требования

- .NET 8 или новее
- PostgreSQL
- Новый или существующий ASP.NET Core проект

## 1. Установка пакетов

```shell
dotnet add package Jobby.Core
dotnet add package Jobby.Postgres
dotnet add package Jobby.AspNetCore
```

## 2. Создание задачи

Создайте команду и обработчик. Jobby выполняет обработчики в фоне.

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

## 3. Настройка Jobby

Добавьте Jobby в `Program.cs`.

`ConnectionStrings__Jobby` — это стандартный ключ строки подключения в ASP.NET Core. Пример значения:
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

// Создание или обновление таблиц Jobby
app.Services.GetRequiredService<IJobbyStorageMigrator>().Migrate();

// Постановка тестовой задачи в очередь при старте приложения
var jobbyClient = app.Services.GetRequiredService<IJobbyClient>();
await jobbyClient.EnqueueCommandAsync(new HelloJob
{
    Message = "Jobby is working"
});

app.MapGet("/", () => "Jobby quick start is running");

app.Run();
```

## 4. Запуск приложения

Задайте строку подключения и запустите приложение:

```shell
ConnectionStrings__Jobby="Host=localhost;Username=postgres;Password=postgres;Database=jobby_quickstart" dotnet run
```

Если используете `appsettings.json`, добавьте:

```json
{
  "ConnectionStrings": {
    "Jobby": "Host=localhost;Username=postgres;Password=postgres;Database=jobby_quickstart"
  }
}
```

## 5. Что должно произойти

При старте Jobby:

- создаст свои таблицы, если их ещё нет
- запустит фоновую обработку
- поставит `HelloJob` в очередь
- почти сразу выполнит её

В логах должно появиться сообщение примерно такого вида:

```text
Job executed. Message: Jobby is working. Attempt: 1
```

## Что дальше

- [Установка и настройка](./install-and-config)
- [Описание задач](./jobs-definition)
- [Постановка задач в очередь](./jobs-enqueue)
- [Задачи по расписанию](./jobs-schedule)
