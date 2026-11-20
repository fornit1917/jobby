# Metrics and Tracing

## Metrics

Jobby can collect several metrics about background job execution separately for each instance running the Jobby server:

- **jobby.inst.jobs.started** - number of jobs started
- **jobby.inst.jobs.completed** - number of successfully completed jobs
- **jobby.inst.jobs.retried** - number of scheduled job retries after errors
- **jobby.inst.jobs.failed** - number of jobs that failed after their last attempt plus the number of failed recurring job runs
- **jobby.inst.jobs.duration** - histogram of job execution durations

To enable metrics collection, call the `UseMetrics` method during configuration:

```csharp
builder.Services.AddJobbyServerAndClient((IAspNetCoreJobbyConfigurable jobbyBuilder) =>
{
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        jobby
            .UseMetrics() // Enable metrics collection
            // ...
    });
});
```

Jobby metrics are added to OpenTelemetry as follows:

```csharp
builder.Services
    .AddOpenTelemetry()
    .WithMetrics(builder => {
        // Add all metrics from Jobby to OpenTelemetry
        builder.AddMeter(JobbyMeterNames.GetAll());
    });
```

In the [Jobby.Samples.AspNet](https://github.com/fornit1917/jobby/tree/master/samples/Jobby.Samples.AspNet) example, metrics collection is enabled with export in Prometheus format via the `/metrics` endpoint.

## Tracing

To enable tracing, call the `UseTracing` method during configuration:

```csharp
builder.Services.AddJobbyServerAndClient(jobbyBuilder =>
{
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        jobby
            .UseTracing() // Run jobs inside an Activity
            // ...
    });
});
```

Calling `UseTracing` results in each background job being executed within a dedicated `Activity` with a new TraceId. The execution span is **named after the job name** (e.g. `DemoJob`), so in trace backends (Jaeger, Tempo, Zipkin) job spans are grouped and searchable by job name; job names are compile-time constants, keeping the span names low-cardinality as OpenTelemetry conventions require.

### Tracing modes

By default every job execution starts a new trace. Optionally, the tracing context of the `Activity` that was current when the job was created can be captured and later reused at job execution. The behaviour is selected with the `JobbyTracingMode` enum:

- **`CreateNewTraceId`** (default) — the execution `Activity` always starts a new trace with a new TraceId.
- **`RestoreTraceId`** — the full W3C traceparent (TraceId, SpanId and sampling flags) of the current `Activity` is captured at job creation; at execution the job `Activity` continues that trace as a child span, so the whole chain "HTTP request → enqueued job → execution" is visible as one trace.
- **`LinkTraceId`** — the current tracing context is captured too, but the execution `Activity` starts an independent trace and references the captured trace via an `ActivityLink`. Useful when you want jobs to be recorded regardless of the sampling decision of the creating request, or when a single trace per job (e.g. across retries) is preferable.

To set the mode globally, pass `JobbyTracingSettings` to `UseTracing`:

```csharp
jobby
    .UseTracing(new JobbyTracingSettings { TracingMode = JobbyTracingMode.RestoreTraceId })
    // ...
```

To set it for a specific job, set `TracingMode` in `JobOpts` (a command can also provide it via `IHasDefaultJobOptions.GetOptionsForEnqueuedJob`). Per-job `JobOpts.TracingMode` overrides the global setting. The per-job mode takes effect only when tracing is enabled with `UseTracing`: without that call jobs are not executed inside an `Activity`, so `TracingMode` has nothing to apply to:

```csharp
// continue the current trace even if the global mode is CreateNewTraceId
jobsFactory.Create(new SendEmailCommand { ... }, new JobOpts { TracingMode = JobbyTracingMode.RestoreTraceId });

// start an independent linked trace for a heavy report
jobsFactory.Create(new HeavyReportCommand { ... }, new JobOpts { TracingMode = JobbyTracingMode.LinkTraceId });
```

Notes:

- Tracing context capture applies to enqueued jobs only. Recurrent (scheduled) jobs always start a new trace on each run.
- `UseTracing` must be called before the jobs factory is created (`CreateJobsFactory` / `CreateJobbyClient`): tracing settings are applied to a factory at its creation, so calling `UseTracing` after that silently leaves the already created factory or client without context capture. The execution middleware itself is registered regardless of the call order.
- If there is no current `Activity` at job creation time, nothing is captured and the execution starts a new trace. The same applies when the current `Activity` uses a non-W3C (hierarchical) ID format — e.g. in apps calling `Activity.ForceDefaultIdFormat = true` — since its IDs cannot form a valid traceparent.
- The captured value is the full W3C traceparent stored in the `traceparent` column of the `jobby_jobs` table; the resolved `JobbyTracingMode` is stored alongside it in the `tracing_mode` column, so the behaviour of a job is pinned at creation time and survives redeployments. Both values are reused across job retries.
- With `RestoreTraceId` the sampling decision of the original trace is preserved: a job created inside an unsampled request will not be recorded either. If you want jobs to be recorded independently of request sampling, use `LinkTraceId`.
- If the stored traceparent is invalid (e.g. corrupted), the execution falls back to starting a new trace.

You can enable export of Jobby job traces through OpenTelemetry as follows:

```csharp
builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => 
        resource.AddService(serviceName: "Jobby.Samples.AspNet"))
    .WithTracing(builder =>
    {
        builder.AddConsoleExporter();

        // Add Jobby job execution traces to OpenTelemetry
        builder.AddSource(JobbyActivitySourceNames.JobsExecution);
    });
```

In the [Jobby.Samples.AspNet](https://github.com/fornit1917/jobby/tree/master/samples/Jobby.Samples.AspNet) example, trace collection is configured with export to stdout provided that the `Jobby.UseTracing` flag in appsettings.json is set to `true`.