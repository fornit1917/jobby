# Установка и настройка

Используйте эту страницу после [быстрого старта](./quickstart). Здесь собраны поддерживаемые варианты подключения и несколько настроек, которые чаще всего нужны в первую очередь.

## Выберите режим подключения

- ASP.NET Core сервер + клиент: используйте, если приложение и ставит задачи в очередь, и обрабатывает их.
- Только ASP.NET Core клиент: используйте, если приложение только создаёт задачи, а выполняет их другой сервис.
- Без ASP.NET Core: используйте для консольных приложений, worker-сервисов и других хостов, где вы сами управляете Jobby.

## ASP.NET Core: сервер + клиент

Это основной вариант для приложения, которое и создаёт, и выполняет задачи.

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

`AddJobbyServerAndClient` регистрирует и `IJobbyClient`, и сервер фоновой обработки. `Migrate()` нужно вызывать до начала обычной работы приложения, чтобы Jobby создал или обновил свои таблицы.

## ASP.NET Core: только клиент

Используйте этот вариант, если сервис только ставит задачи в очередь.

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

Такой вариант регистрирует `IJobbyClient`, но не запускает фоновую обработку. Используйте его, если задачи исполняются в другом приложении.

## Без ASP.NET Core

Для консольных приложений и других нестандартных хостов Jobby настраивается напрямую через `JobbyBuilder`.

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

В этом режиме вы сами управляете жизненным циклом: передаёте фабрику execution scope, запускаете миграции, создаёте клиент и сервер, а также явно стартуете и останавливаете фоновую обработку.

## Основные настройки

Большинству приложений достаточно настроить несколько вещей.

### Строка подключения и Data Source

Используйте стандартную строку подключения ASP.NET Core, например `ConnectionStrings:Jobby`, и создавайте один общий `NpgsqlDataSource`.

### PostgreSQL: схема и префикс таблиц

Если Jobby использует общую базу данных вместе с другими данными, можно изолировать его объекты через отдельную схему или префикс таблиц:

```csharp
config.UsePostgresql(pg =>
{
    pg.UseDataSource(sp.GetRequiredService<NpgsqlDataSource>());
    pg.UseSchemaName("jobs");
    pg.UseTablesPrefix("jobby_");
});
```

Это удобно, если вы хотите явно отделить таблицы Jobby от остальных объектов базы.

### Настройки сервера

`JobbyServerSettings` определяет, насколько активно Jobby читает и выполняет задачи.

- `MaxDegreeOfParallelism`: сколько задач может выполняться одновременно. Увеличивайте для большей пропускной способности, уменьшайте для тяжёлых задач или ограниченных ресурсов.
- `TakeToProcessingBatchSize`: сколько задач Jobby забирает из очереди за один раз. Большие значения повышают throughput, маленькие делают нагрузку ровнее.
- `PollingIntervalMs`: как часто сервер проверяет наличие новых задач. Меньше значение снижает задержку, но увеличивает частоту запросов к базе.

### Политика повторов

`RetryPolicy` задаёт поведение после ошибки выполнения.

- `MaxCount`: максимальное число попыток запуска.
- `IntervalsSeconds`: задержки между повторными попытками.

Начните с небольшого числа повторов и коротких интервалов, а затем подстройте их под типичные временные сбои в вашей системе.

## Что дальше

- [Описание задач](./jobs-definition)
- [Постановка задач в очередь](./jobs-enqueue)
- [Задачи по расписанию](./jobs-schedule)
- [Настройка повторов](./retry-policies)
- [Мульти-очереди](./multiqueues)
- [Наблюдаемость](./observability)
- [Middleware](./middlewares)
- [Отказоустойчивость](./fault-tolerance)
