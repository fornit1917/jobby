using Jobby.AspNetCore;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services.Observability;
using Jobby.Postgres.ConfigurationExtensions;
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

        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var connectionString = "Host=localhost;Username=test_user;Password=12345;Database=test_db";
        builder.Services.AddSingleton<NpgsqlDataSource>(NpgsqlDataSource.Create(connectionString));

        builder.Services.AddDbContext<JobbySampleDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>());
            opts.UseSnakeCaseNamingConvention();
        });

        builder.Services.AddScoped<JobLoggingMiddleware>();
        const string recurrentJobsQueueName = "recurrent";
        builder.Services.AddJobbyServerAndClient((IAspNetCoreJobbyConfigurable jobbyBuilder) =>
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
                        PollingIntervalMs = 500,
                        MaxDegreeOfParallelism = 10,
                        TakeToProcessingBatchSize = 10,
                        Queues = [
                            new QueueSettings { QueueName = QueueSettings.DefaultQueueName },
                            new QueueSettings { QueueName = recurrentJobsQueueName }
                        ]
                    })
                    .UseDefaultRetryPolicy(new RetryPolicy
                    {
                        MaxCount = 3,
                        IntervalsSeconds = [1, 2]
                    })
                    .UseScheduler(new CustomSecondsStorage())
                    .ConfigurePipeline(pipeline =>
                    {   
                        // Some custom middlewares
                        pipeline.Use<JobLoggingMiddleware>(); // will be created by DI Scope
                        pipeline.Use(new IgnoreSomeErrorsMiddleware()); // will be used this instance always
                    });
                
                if (appJobbyConfig.UseMetrics)
                {
                    // Enable collecting metrics
                    jobby.UseMetrics();
                }

                if (appJobbyConfig.UseTracing)
                {
                    // Enable tracing context for each job run
                    jobby.UseTracing();
                }
            });
        });

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName: "Jobby.Samples.AspNet"))
            .WithMetrics(metricsBuilder => {
                metricsBuilder.AddPrometheusExporter();

                // Add metrics from Jobby to OpenTelemetry
                metricsBuilder.AddMeter(JobbyMeterNames.GetAll());
            })
            .WithTracing(tracingBuilder =>
            {
                tracingBuilder.AddConsoleExporter();

                // Add traces from Jobby jobs execution to OpenTelemetry
                tracingBuilder.AddSource(JobbyActivitySourceNames.JobsExecution);
            });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics");
        app.UseAuthorization();
        app.MapControllers();
        
        // Create or update jobby storage schema
        var jobbyStorageMigrator = app.Services.GetRequiredService<IJobbyStorageMigrator>();
        jobbyStorageMigrator.Migrate();

        // Add recurrent jobs
        var jobbyClient = app.Services.GetRequiredService<IJobbyClient>();
        jobbyClient.ScheduleRecurrent(new EmptyRecurrentJobCommand(), "*/5 * * * * *");

        app.Run();
    }
}
