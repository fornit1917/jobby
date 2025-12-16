# Jobby

Производительная и надёжная .net-библиотека для фоновых задач, адаптированная для использования в распределённых приложениях.

## Ключевые возможности и особенности

- Задачи по расписанию
- Выполнение задач из очереди
- Транзакционное создание нескольких задач
- Настройка порядка выполнения при создании нескольких задач
- Повтор упавших задач согласно настроенным политикам повторов
- Настраиваемый middlewares pipeline для исполнения кода фоновых задач
- Совместимые с OpenTelemetry метрики и трейсинг
- Корректная работа в распределённых приложениях
- Устойчивость к сбоям и отказу компонентов
- Высокая производительность
- Низкое потребление ресурсов на стороне .net-приложения и базы данных

## Инструкция по применению

### Установка

Для использования необходимо установить пакет Jobby.Core и пакет для работы с хранилищем задач (на данный момент поддерживается только
PostgreSQL):

```
dotnet package add Jobby.Core
dotnet package add Jobby.Postgres
```

Для интеграции библиотеки в AspNetCore-приложение также потребуется пакет Jobby.AspNetCore:

```
dotnet package add Jobby.AspNetCore
```

#### Создание таблиц в базе данных

Для создания необходимых таблиц в базе данных необходимо выполнить [скрипт](https://github.com/fornit1917/jobby/blob/master/src/Jobby.Postgres/jobby.sql).

### Описание фоновой задачи

Для описания фоновой задачи в вашем куда нужно реализовать интерфейс `IJobCommand` для объекта-параметра задачи 
и интерфейс `IJobCommandHandler` непосредственно для кода задачи:

```csharp
public class SendEmailCommand : IJobCommand
{
    // В свойствах можно указать любые параметры, которые будут переданы в задачу
    public string Email { get; init; }

    // Из этого метода нужно вернуть любое уникальное название идентифицирующее задачу
    public static string GetJobName() => "SendEmail";

    // Из этого метода нужно вернуть флаг, указывающий допустимо ли автоматически перезапускать задачу
    // если выполняющий её сервер предположительно вышел из строя.
    // Рекомендуется возвращать true только для идемпотентных задач.
    public bool CanBeRestarted() => true;
}

public class SendEmailCommandHandler : IJobCommandHandler<SendEmailCommand>
{
    // При использовании пакет Jobby.AspNetCore поддерживается внедрение зависимостей из DI-контейнера
    private readonly IEmailService _emailService;
    public SendEmailCommandHandler(IEmailService logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(SendEmailCommand command, JobExecutionContext ctx)
    {
        // Здесь вы можете написать код, выполняющий вашу задачу
        // command - параметры задачи
        // ctx - содержит cancelationToken и дополнительную информацию о выполнении задачи
    }
}
```

### Настройка библиотеки

#### Настройка в asp.net core

Для добавления Jobby в AspNetCore-приложение необходимо использовать метод-расширение `AddJobbyServerAndClient`,
который добавит в DI-контейнер сервисы для создания задач, а также добавит background-сервис для их выполнения.

```csharp
builder.Services.AddSingleton<NpgsqlDataSource>(NpgsqlDataSource.Create(databaseConnectionString));

builder.Services.AddJobbyServerAndClient(jobbyBuilder =>  
{
    // Указать сборки, содержащие код ваших реализаций IJobCommand и IJobCommandHandler

    jobbyBuilder.AddJobsFromAssemblies(typeof(SendEmailCommand).Assembly);

    // Настройка jobby
    jobbyBuilder.ConfigureJobby((serviceProvider, jobby) => {
        jobby.UsePostgresql(serviceProvider.GetRequiredService<NpgsqlDataSource>());  
        jobby.UseServerSettings(new JobbyServerSettings  
        {  
            // Максимальное количество одновременно выполняемых задач
            MaxDegreeOfParallelism = 10,  

            // Максимальное количество задач, извлекаемых из очереди за один запрос
            TakeToProcessingBatchSize = 10,  
        });
        jobby.UseDefaultRetryPolicy(new RetryPolicy  
        {  
            // Максимальное количество попыток запуска задачи  
            MaxCount = 3,  

            // Паузы между попытками запуска в случае провала задач
            IntervalsSeconds = [1, 2]
        });
    }); 
});
```

Полный пример использования Jobby в AspNetCore-приложении доступен здесь: [Jobby.Samples.AspNet](https://github.com/fornit1917/jobby/tree/master/samples/Jobby.Samples.AspNet).

#### Настройка без использования asp.net core

При использовании библиотеки без интеграции с AspNetCore, необходимо создать объект класса `JobbyBuilder`, который настраивается подобно примеру выше. Затем из этого объекта можно получить сервисы для добавления задач и объект для запуска background-сервиса для их выполнения.

Однако если вы не используете AspNetCore, вам необходимо самим реализовать фабрику для ваших объектов `IJobHandler<T>`.

```csharp
var jobbyBuilder = new JobbyBuilder();
jobbyBuilder
        .UsePostgresql(dataSource)
        // scopeFactory - экземпляр вашей реализации фабрики
        .UseExecutionScopeFactory(scopeFactory)
        .AddJobsFromAssemblies(typeof(SendEmailCommand).Assembly);

// Сервис который вы можете использовать для создания задач
var jobbyClient = builder.CreateJobbyClient();

// Фоновый сервис выполнения задач
var jobbyServer = builder.CreateJobbyServer();
jobbyServer.StartBackgroundService(); // Запуск фонового сервиса
//...
jobbyServer.SendStopSignal(); // Остановка
```

Полный пример использования Jobby в простом консольном приложении без AspNetCore и пример реализации собственной фабрики доступны здесь: [Jobby.Samples.CliJobsSample](https://github.com/fornit1917/jobby/tree/master/samples/Jobby.Samples.CliJobsSample).

### Добавление задачи в очередь на запуск

Для добавления задач в очередь используется сервис `IJobbyClient`, экземпляр которого в случае использования AspNetCore можно получить
из DI-контейнера, или из `JobbyBuilder` в ином случае.

#### Добавление одной задачи

```csharp
var command = new SendEmailCommand { Email = "some@email.com" };

// Добавить задачу в очередь для выполнения так скоро, насколько возможно
await jobbyClient.EnqueueCommandAsync(command); 

// Добавить задачу в очередь для выполнения не ранее указанного времени
await jobbyClient.EnqueueCommandAsync(command, DateTime.UtcNow.AddHours(1));
```

#### Добавление нескольких задач

Для добавления в одной транзакции нескольких задач нужно подготовить для них список объектов `JobCreationModel` с помощью фабрики, доступной в `jobbyClient.Factory`, а затем вызвать метод `EnqueueBatchAsync`:

```csharp
var jobs = new List<JobCreationModel>
{
    jobbyClient.Factory
        .Create(new SendEmailCommand { Email = "first@email.com" }),
    
    jobbyClient.Factory
        .Create(new SendEmailCommand { Email = "second@email.com" }),
}

await jobbyClient.EnqueueBatchAsync(jobs);
```

При добавлении нескольких задач существует опция настроить для них строгий порядок выполнения:

```csharp
var sequenceBuilder = jobbyClient.Factory.CreateSequenceBuilder();

// Задачи будут выполнены в строго указанном ниже порядке
sequenceBuilder.Add(jobbyClient.Factory
    .Create(new SendEmailCommand { Email = "first@email.com" }));

sequenceBuilder.Add(jobbyClient.Factory
    .Create(new SendEmailCommand { Email = "second@email.com" }));

var jobs = sequenceBuilder.GetJobs();

await jobbyClient.EnqueueBatchAsync(jobs);
```

#### Использование EntityFramework для добавления задач

Если в вашем проекте используется EntityFramework, то вы можете для добавления задач использовать ваш DbContext.
Это может быть полезно, если в одной транзакции вам необходимо и поставить задачу в очередь, и выполнить операцию
над некоторой доменной сущностью.

```csharp
public class YourDbContext : DbContext
{
    // Нужно добавить DbSet для сущности JobCreationModel
    public DbSet<JobCreationModel> Jobs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobCreationModel>().ToTable("jobby_jobs");
        modelBuilder.Entity<JobCreationModel>().HasKey(x => x.Id);
        // Для остальной настройки необходимо применить skake_case naming convention
    }
}

// Постановка задачи в очередь через EF
var command = new SendEmailCommand { Email = "some@email.com" };
var jobEntity = jobbyClient.Factory.Create(command);
_dbContext.Jobs.Add(job);
await _dbContext.SaveChangesAsync();
```

Пример использования EF доступен здесь: [Jobby.Samples.AspNet](https://github.com/fornit1917/jobby/tree/master/samples/Jobby.Samples.AspNet).

### Задачи по расписанию

```csharp
// Код задачи по расписанию описывается аналогично обычной задаче

public class RecurrentJobCommand : IJobCommand
{
    public static string GetJobName() => "SomeRecurrentJob";
    public bool CanBeRestarted() => true;
}

public class RecurrentJobHandler : IJobCommandHandler<RecurrentJobCommand>
{
    public async Task ExecuteAsync(SendEmailCommand command, JobExecutionContext ctx)
    {
        // Код вашей задачи по расписанию
    }
}

// Установка расписания для задачи (используется cron expression)
// После этого задача будет автоматически запускаться каждые 5 минут
var command = new RecurrentJobCommand();
await jobbyClient.ScheduleRecurrentAsync(command, "*/5 * * * *");
```

### Настройка политики повторов

Если при выполнении задачи произошла ошибка, она может быть повторена в соответствии с настроенной политикой повторов.

Политика повторов - это объект `RetryPolicy`, который содержит общее максимальное количество попыток запуска, а так же
интервалы в секундах, которые будут сделаны между попытками повтора.

```csharp
var retryPolicy = new RetryPolicy
{
    // Максимальное общее количество попыток запуска задачи
    // Значени 3 значит, что если задачу не удалось выполнить первом запуске,
    // для неё будет выполнено еще две попытки повтора
    MaxCount = 3,

    // Паузы между попытками запуска в случае провала задач
    // Первый повтор будет через одну секунду, второй через две
    IntervalsSeconds = [1, 2]

    
}

// В IntervalSeconds не обязательно указывать все значения
// Например если мы хотим повторять задачу 10 раз каждые 10 минут, доступен следующий вариант
retryPolicy = new RetryPolicy
{
    MaxCount = 11,
    IntervalsSeconds = [600]
}
```

Политика повторов может быть глобальной (применяемой ко всех задачам) или же уникальной для конкретного типа задач.
Это указывается при конфигурации библиотеки:

```csharp
jobbyBuilder
    // по умолчанию будет применятся политика из объекта defaultPolicy
    .UseDefaultRetryPolicy(defaultPolicy)
    // но для задач SendEmailCommand будет применяться другая политика
    .UseRetryPolicyForJob<SendEmailCommand>(specialRetryPolicy);
```

### Использование Middlewares

Вызов обработчиков фоновых задач возможно оборачивать в свои Middlewares.

Для создания Middleware необходимо реализовать интерфейс IJobbyMiddleware:

```csharp
public class SomeMiddleware : IJobbyMiddleware
{
    public async Task ExecuteAsync<TCommand>(TCommand command, JobExecutionContext ctx, IJobCommandHandler<TCommand> handler)
        where TCommand : IJobCommand
    {
        // Здесь можно разместить логику которая выполнится до вызова фоновой задачи
        // ....

        await handler.ExecuteAsync(command, ctx);

        // Здесь можно разместить логику которая выполнится после вызова фоновой задачи
        // .... 
    }
}
```

Для Middleware поддерживается инъекция зависимостей через конструктор.

Подключение:

```csharp
builder.Services.AddJobbyServerAndClient(jobbyBuilder =>  
{
    jobbyBuilder.ConfigureJobby((serviceProvider, jobby) => {
        // ...
        jobby.ConfigurePipeline(pipeline => {

            // Так подключается singleton middleware без зависимостей
            pipeline.Use(new SomeMiddleware());

            // Так можно подключать singleton middleware с не scoped-зависимостями
            // В этом случае тип SomeMiddleware должен быть зарегистрирован в DI-контейнере!
            pipeline.Use(serviceProvider.GetRequiredService<SomeMiddleware>());

            // Так можно подключать scoped middleware или middleware со scoped зависимостями
            // В этом случае тип SomeMiddleware должен быть зарегистрирован в DI-контейнере!
            pipeline.Use<SomeMiddleware>();
        });
    }); 
});
```

Больше примеров здесь: [Jobby.Samples.AspNet](https://github.com/fornit1917/jobby/tree/master/samples/Jobby.Samples.AspNet).

### Метрики

Jobby собирает несколько метрик о выполнении фоновых задач на данном инстансе:

- jobby.inst.jobs.started - количество запущенных задач
- jobby.inst.jobs.completed - количество успешно выполненных задач
- jobby.inst.jobs.retried - количество запланированных повторов задач после ошибки
- jobby.inst.jobs.failed - количество упавших после последней попытки задач плюс количество неудачных запусков рекуррентных задач
- jobby.inst.jobs.duration - гистограмма времени выполнения задач

Для включения сбора метрик необходимо при конфигурации вызвать метод `UseMetrics`:

```csharp
builder.Services.AddJobbyServerAndClient((IAspNetCoreJobbyConfigurable jobbyBuilder) =>
{
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        jobby
            .UseMetrics() // Включить сбор метрик
            // ...
    });
});
```

В OpenTelemetry метрики Jobby добавляются следующим образом:

```csharp
builder.Services
    .AddOpenTelemetry()
    .WithMetrics(builder => {
        // Добавить в OpenTelemetry все метрики от Jobby
        builder.AddMeter(JobbyMeterNames.GetAll());
    });
```

В примере [Jobby.Samples.AspNet](https://github.com/fornit1917/jobby/tree/master/samples/Jobby.Samples.AspNet) включен сбор метрик с экспортом в формате Prometheus через эндпоинт `/metrics`.

### Трейсинг

Для включения трейсинга необходимо при конфигурации вызвать метод `UseTracing`:

```csharp
builder.Services.AddJobbyServerAndClient((IAspNetCoreJobbyConfigurable jobbyBuilder) =>
{
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        jobby
            .UseTracing() // Запускать джобы внутри Activity
            // ...
    });
});
```

Включить экспорт трейсов по джобам Jobby через OpenTelemetry можно следующим образом:

```csharp
builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName: "Jobby.Samples.AspNet"))
    .WithTracing(builder =>
    {
        builder.AddConsoleExporter();

        // Добавить в OpenTelemetry трейсы выполнения джобов Jobby 
        builder.AddSource(JobbyActivitySourceNames.JobsExecution);
    });
```

В примере [Jobby.Samples.AspNet](https://github.com/fornit1917/jobby/tree/master/samples/Jobby.Samples.AspNet) включен сбор метрик с экспортом в stdout.