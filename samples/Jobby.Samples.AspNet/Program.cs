
using Jobby.AspNetCore;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Postgres.ConfigurationExtensions;
using Jobby.Samples.AspNet.Jobs;
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
        var dataSource = NpgsqlDataSource.Create(connectionString);
        builder.Services.AddJobby(jobby =>
        {
            jobby
                .UsePostgresql(dataSource)
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
                })
                .AddJobsFromAssemblies(typeof(DemoJobCommand).Assembly);
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
