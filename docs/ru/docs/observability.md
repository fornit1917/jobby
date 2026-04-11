# Метрики и трейсинг

## Метрики

Jobby может собирать несколько метрик о выполнении фоновых задач отдельно для каждого инстанса, на котором запущен Jobby-сервер:

- **jobby.inst.jobs.started** - количество запущенных задач
- **jobby.inst.jobs.completed** - количество успешно выполненных задач
- **jobby.inst.jobs.retried** - количество запланированных повторов задач после ошибки
- **jobby.inst.jobs.failed** - количество упавших после последней попытки задач плюс количество неудачных запусков рекуррентных задач
- **jobby.inst.jobs.duration** - гистограмма времени выполнения задач

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

## Трейсинг

Для включения трейсинга необходимо при конфигурации вызвать метод `UseTracing`:

```csharp
builder.Services.AddJobbyServerAndClient(jobbyBuilder =>
{
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        jobby
            .UseTracing() // Запускать джобы внутри Activity
            // ...
    });
});
```

Вызов `UseTracing` приводит к тому, что каждая фоновая задача выполняется внутри выделенной `Activity` с новым TraceId. Захват TraceId при создании фоновых задач
на данный момент не поддерживается, но это возможность планируется к добавлению в будущих версиях Jobby.

Включить экспорт трейсов по джобам Jobby через OpenTelemetry можно следующим образом:

```csharp
builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => 
        resource.AddService(serviceName: "Jobby.Samples.AspNet"))
    .WithTracing(builder =>
    {
        builder.AddConsoleExporter();

        // Добавить в OpenTelemetry трейсы выполнения джобов Jobby 
        builder.AddSource(JobbyActivitySourceNames.JobsExecution);
    });
```

В примере [Jobby.Samples.AspNet](https://github.com/fornit1917/jobby/tree/master/samples/Jobby.Samples.AspNet) настроен сбор трейсов с экспортом в stdout при условии
что флаг `Jobby.UseTracing` из appsettings.json установлен в `true`.