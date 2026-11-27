using Jobby.AspNetCore;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services.Observability;
using Jobby.Dashboard;
using Jobby.Dashboard.Authorization.Basic;
using Jobby.Postgres.ConfigurationExtensions;
using Jobby.Postgres.Dashboard;
using Jobby.Samples.AspNet.DashboardDemo;
using Jobby.Samples.AspNet.Db;
using Jobby.Samples.AspNet.Jobs;
using Jobby.Samples.AspNet.JobsMiddlewares;
using Jobby.Samples.AspNet.Schedulers;
using Jobby.Samples.AspNet.Settings;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Jobby.Samples.AspNet;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var appJobbyConfig = new AppJobbySettings();
        builder.Configuration.Bind("Jobby", appJobbyConfig);

        builder.Logging.AddConsole();

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        const string connectionString = "Host=localhost;Username=jobby;Password=jobby;Database=jobby_tests_db;GSS Encryption Mode=Disable";
        var dataSource = NpgsqlDataSource.Create(connectionString);
        builder.Services.AddSingleton(dataSource);

        var dashboardStorageOptions = new PostgresqlDashboardStorageOptions
        {
            SchemaName = "",
            TablesPrefix = "jobby_",
        };
        builder.Services.AddSingleton(dashboardStorageOptions);
        builder.Services.AddSingleton<DashboardDemoSeeder>();

        builder.Services.AddJobbyDashboard()
            .AddBasicAuth(o =>
            {
                o.Username = "admin";
                o.PasswordHash = PasswordHasher.Hash("s3cret");
            });

        builder.Services.AddJobbyPostgresDashboardStorage(dataSource, o =>
        {
            o.SchemaName = dashboardStorageOptions.SchemaName;
            o.TablesPrefix = dashboardStorageOptions.TablesPrefix;
        });

        builder.Services.AddDbContext<JobbySampleDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>());
            opts.UseSnakeCaseNamingConvention();
        });

        builder.Services.AddScoped<JobLoggingMiddleware>();
        const string recurrentJobsQueueName = "recurrent";
        var queues = appJobbyConfig.Queues.Length > 0
            ? appJobbyConfig.Queues.Select(q => new QueueSettings
            {
                QueueName = q.QueueName,
                MaxDegreeOfParallelism = q.MaxDegreeOfParallelism,
                DisableSerializableGroups = q.DisableSerializableGroups,
            }).ToArray()
            : [new QueueSettings { QueueName = QueueSettings.DefaultQueueName }];
        builder.Services.AddJobbyServerAndClient(jobbyBuilder =>
        {
            jobbyBuilder
                .AddJobsFromAssemblies(typeof(DemoJobCommand).Assembly)
                .UseQueueForAllRecurrent(recurrentJobsQueueName);

            jobbyBuilder.ConfigureJobby((sp, jobby) =>
            {
                jobby
                    .UsePostgresql(sp.GetRequiredService<NpgsqlDataSource>())
                    .UseServerSettings(new JobbyServerSettings
                    {
                        PollingIntervalMs = appJobbyConfig.Server.PollingIntervalMs,
                        PollingIntervalStartMs = appJobbyConfig.Server.PollingIntervalStartMs,
                        PollingIntervalFactor = appJobbyConfig.Server.PollingIntervalFactor,
                        MaxDegreeOfParallelism = appJobbyConfig.Server.MaxDegreeOfParallelism,
                        TakeToProcessingBatchSize = appJobbyConfig.Server.TakeToProcessingBatchSize,
                        DeleteCompleted = appJobbyConfig.Server.DeleteCompleted,
                        CompleteWithBatching = appJobbyConfig.Server.CompleteWithBatching,
                        DisableSerializableGroups = appJobbyConfig.Server.DisableSerializableGroups,
                        HeartbeatIntervalSeconds = appJobbyConfig.Server.HeartbeatIntervalSeconds,
                        MaxNoHeartbeatIntervalSeconds = appJobbyConfig.Server.MaxNoHeartbeatIntervalSeconds,
                        PermanentLockedFreezingIntervalSeconds = appJobbyConfig.Server.PermanentLockedFreezingIntervalSeconds,
                        PermanentLockedHandleUnlockingRequestsIntervalSeconds = appJobbyConfig.Server.PermanentLockedHandleUnlockingRequestsIntervalSeconds,
                        DbErrorPauseMs = appJobbyConfig.Server.DbErrorPauseMs,
                        Queues = queues
                    })
                    .UseDefaultRetryPolicy(new RetryPolicy
                    {
                        MaxCount = 3,
                        IntervalsSeconds = [1, 2]
                    })
                    .UseScheduler(new SecondsIntervalScheduleHandler())
                    .ConfigurePipeline(pipeline =>
                    {
                        pipeline.Use<JobLoggingMiddleware>();
                        pipeline.Use(new IgnoreSomeErrorsMiddleware());
                    });

                if (appJobbyConfig.UseMetrics)
                {
                    jobby.UseMetrics();
                }

                if (appJobbyConfig.UseTracing)
                {
                    // Enable tracing context for each job run
                    jobby.UseTracing(new JobbyTracingSettings { TracingMode = appJobbyConfig.TracingMode });
                }
            });
        });

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName: "Jobby.Samples.AspNet"))
            .WithMetrics(metricsBuilder =>
            {
                metricsBuilder.AddPrometheusExporter();

                metricsBuilder.AddMeter(JobbyMeterNames.GetAll());
            })
            .WithTracing(tracingBuilder =>
            {
                tracingBuilder.AddConsoleExporter();

                // Create an Activity for each incoming HTTP request,
                // so jobs enqueued during a request can inherit its TraceId
                tracingBuilder.AddAspNetCoreInstrumentation();

                // Add traces from Jobby jobs execution to OpenTelemetry
                tracingBuilder.AddSource(JobbyActivitySourceNames.JobsExecution);
            });

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics");
        app.UseAuthorization();
        app.MapControllers();
        app.MapJobbyDashboard("/jobby");

        var jobbyStorageMigrator = app.Services.GetRequiredService<IJobbyStorageMigrator>();
        jobbyStorageMigrator.Migrate();

        var jobbyClient = app.Services.GetRequiredService<IJobbyClient>();
        jobbyClient.ScheduleRecurrent(new EmptyRecurrentJobCommand(), "*/5 * * * * *");

        if (appJobbyConfig.SeedDashboardDemoData)
        {
            app.Services.GetRequiredService<DashboardDemoSeeder>()
                .SeedAsync()
                .GetAwaiter()
                .GetResult();
        }

        app.Run();
    }
}
