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

Вызов `UseTracing` приводит к тому, что каждая фоновая задача выполняется внутри выделенной `Activity` с новым TraceId. Span выполнения **именуется именем задачи** (например, `DemoJob`), поэтому в трейсинг-бэкендах (Jaeger, Tempo, Zipkin) span'ы задач группируются и ищутся по имени задачи; имена задач — константы времени компиляции, что сохраняет низкую кардинальность имён span'ов, как требуют конвенции OpenTelemetry.

### Режимы трейсинга

По умолчанию каждый запуск задачи порождает новый трейс. Опционально можно захватить контекст трейсинга `Activity`, актуальной в момент создания обычной (enqueued) задачи, и использовать его при выполнении. Поведение выбирается enum `JobbyTracingMode`:

- **`CreateNewTraceId`** (по умолчанию) — `Activity` выполнения всегда порождает новый трейс с новым TraceId.
- **`RestoreTraceId`** — в момент создания задачи захватывается полный W3C traceparent (TraceId, SpanId и флаги сэмплирования) текущей `Activity`; при выполнении `Activity` задачи продолжает этот трейс как дочерний span. Так вся цепочка «HTTP-запрос → создание задачи → её выполнение» видна как один трейс.
- **`LinkTraceId`** — текущий контекст трейсинга тоже захватывается, но `Activity` выполнения порождает независимый трейс и ссылается на захваченный через `ActivityLink`. Полезно, когда нужно, чтобы задачи записывались в телеметрию независимо от решения сэмплера породившего запроса, или когда желателен отдельный трейс на задачу (например, при ретраях).

Чтобы задать режим глобально, передайте `JobbyTracingSettings` в `UseTracing`:

```csharp
jobby
    .UseTracing(new JobbyTracingSettings { TracingMode = JobbyTracingMode.RestoreTraceId })
    // ...
```

Чтобы задать режим для конкретной задачи, укажите `TracingMode` в `JobOpts` (команда также может предоставить его через `IHasDefaultJobOptions.GetOptionsForEnqueuedJob`). Значение в `JobOpts.TracingMode` имеет приоритет над глобальной настройкой. Per-job режим действует только при включённом трейсинге через `UseTracing`: без этого вызова задачи не выполняются внутри `Activity`, поэтому `TracingMode` не к чему применять:

```csharp
// продолжить текущий трейс, даже если глобально установлен CreateNewTraceId
jobsFactory.Create(new SendEmailCommand { ... }, new JobOpts { TracingMode = JobbyTracingMode.RestoreTraceId });

// запустить независимый связанный трейс для тяжёлого отчёта
jobsFactory.Create(new HeavyReportCommand { ... }, new JobOpts { TracingMode = JobbyTracingMode.LinkTraceId });
```

Примечания:

- Захват контекста трейсинга действует только для обычных задач. Задачи по расписанию (рекуррентные) всегда порождают новый TraceId на каждом запуске.
- `UseTracing` нужно вызывать до создания фабрики задач (`CreateJobsFactory` / `CreateJobbyClient`): настройки трейсинга применяются к фабрике в момент её создания, поэтому вызов `UseTracing` после этого молча оставит уже созданную фабрику или клиент без захвата контекста. Сам middleware выполнения регистрируется независимо от порядка вызовов.
- Если в момент создания задачи нет текущей `Activity`, захватывать нечего — выполнение начнётся с новым TraceId. То же самое происходит, если текущая `Activity` использует не-W3C (иерархический) формат идентификаторов — например, в приложениях, вызывающих `Activity.ForceDefaultIdFormat = true`, — поскольку её идентификаторы не могут образовать валидный traceparent.
- Захваченное значение — это полный W3C traceparent, который сохраняется в колонке `traceparent` таблицы `jobby_jobs`; рядом в колонке `tracing_mode` сохраняется выбранный `JobbyTracingMode`, поэтому поведение задачи фиксируется в момент создания и не меняется при передеплое. Оба значения используются при всех ретраях задачи.
- В режиме `RestoreTraceId` решение сэмплирования исходного трейса сохраняется: задача, созданная в незаписанном (unsampled) запросе, тоже не будет записана. Если нужно, чтобы задачи записывались независимо от сэмплирования запросов, используйте `LinkTraceId`.
- Если сохранённый traceparent невалиден (например, повреждён), выполнение откатывается к созданию нового трейса.

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

В примере [Jobby.Samples.AspNet](https://github.com/fornit1917/jobby/tree/master/samples/Jobby.Samples.AspNet) настроен сбор трейсов с экспортом в stdout при условии,
что флаг `Jobby.UseTracing` из appsettings.json установлен в `true`.