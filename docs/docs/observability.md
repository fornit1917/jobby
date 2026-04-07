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

Calling `UseTracing` results in each background job being executed within a dedicated `Activity` with a new TraceId. Capturing TraceId when creating background jobs is currently not supported, but this capability is planned for future versions of Jobby.

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