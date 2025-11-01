
using Jobby.AspNetCore;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Postgres.ConfigurationExtensions;
using Jobby.Samples.AspNet.Db;
using Jobby.Samples.AspNet.Jobs;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Jobby.Samples.AspNet;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

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

        builder.Services.AddJobbyServerAndClient((IAspNetCoreJobbyConfigurable jobbyBuilder) =>
        {
            jobbyBuilder.AddJobsFromAssemblies(typeof(DemoJobCommand).Assembly);
            jobbyBuilder.ConfigureJobby((sp, jobby) =>
            {
                jobby
                    .UsePostgresql(sp.GetRequiredService<NpgsqlDataSource>())
                    .UseServerSettings(new JobbyServerSettings
                    {
                        PollingIntervalMs = 500,
                        MaxDegreeOfParallelism = 10,
                        TakeToProcessingBatchSize = 10,
                    })
                    .UseDefaultRetryPolicy(new RetryPolicy
                    {
                        MaxCount = 3,
                        IntervalsSeconds = [1, 2]
                    });
            });
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthorization();
        app.MapControllers();

        // Add reccurent jobs
        var jobbyClient = app.Services.GetRequiredService<IJobbyClient>();
        jobbyClient.ScheduleRecurrent(new EmptyRecurrentJobCommand(), "*/5 * * * * *");

        app.Run();
    }
}
