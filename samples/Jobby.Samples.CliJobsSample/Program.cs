using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.Postgres.ConfigurationExtensions;
using Jobby.Samples.CliJobsSample.HandlerFactory;
using Jobby.Samples.CliJobsSample.Jobs;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.Json;

namespace Jobby.Samples.CliJobsSample;

internal class Program
{
    static void Main(string[] args)
    {
        var connectionString = "Host=localhost;Username=test_user;Password=12345;Database=test_db";
        using var dataSource = NpgsqlDataSource.Create(connectionString);

        var loggerFactory = LoggerFactory.Create(x => x.AddConsole());
        var jsonOptions = new JsonSerializerOptions();
        var jobbySettings = new JobbyServerSettings
        {
            PollingIntervalMs = 1000,
            PollingIntervalStartMs = 50,
            PollingIntervalFactor = 2,
            DbErrorPauseMs = 5000,
            MaxDegreeOfParallelism = 10,
            TakeToProcessingBatchSize = 10,
            DeleteCompleted = true,
            CompleteWithBatching = true,
            HeartbeatIntervalSeconds = 3,
            MaxNoHeartbeatIntervalSeconds = 10
        };
        var scopeFactory = new SimpleJobExecutionScopeFactory();
        var defaultRetryPolicy = new RetryPolicy
        {
            MaxCount = 3,
            IntervalsSeconds = [1]
        };

        var builder = new JobbyServicesBuilder();
        builder
            .UsePostgresql(pgOpts =>
            {
                pgOpts.UseDataSource(dataSource);
            })
            .UseServerSettings(jobbySettings)
            .UseSystemTextJson(jsonOptions)
            .UseExecutionScopeFactory(scopeFactory)
            .UseDefaultRetryPolicy(defaultRetryPolicy)
            .UseLoggerFactory(loggerFactory)
            .AddJobsFromAssemblies(typeof(TestCliJobCommand).Assembly);

        var jobbyServer = builder.CreateJobbyServer();
        var jobbyClient = builder.CreateJobbyClient();

        Console.WriteLine("1. Enqueue success jobs");
        Console.WriteLine("2. Enqueue failed job");
        Console.WriteLine("3. Enqueue jobs sequence");
        Console.WriteLine("4. Schedule recurrent job");
        Console.WriteLine("5. Cancel recurrent job");

        string? action = Console.ReadLine();

        switch (action)
        {
            case "1":
                CreateSuccess(jobbyClient, 5);
                break;
            case "2":
                CreateFailed(jobbyClient);
                break;
            case "3":
                CreateSequence(jobbyClient, 5);
                break;
            case "4":
                CreateRecurrent(jobbyClient);
                break;
            case "5":
                CancelRecurrent(jobbyClient);
                break;
        }


        Console.Write("Start server? (y/n): ");
        var runServer = Console.ReadLine();
        if (runServer != "n")
            jobbyServer.StartBackgroundService();

        Console.ReadLine();

        jobbyServer.SendStopSignal();
    }

    private static void CreateSuccess(IJobbyClient jobbtClient, int jobsCount)
    {
        for (int i = 1; i <= jobsCount; i++)
        {
            var jobParam = new TestCliJobCommand
            {
                Id = i,
                ShouldBeFailed = false,
                Name = "SomeValue"
            };
            jobbtClient.EnqueueCommand(jobParam);
        }
    }

    private static void CreateFailed(IJobbyClient jobbyClient)
    {
        var jobParam = new TestCliJobCommand
        {
            Id = 500,
            ShouldBeFailed = true,
            Name = "SomeValue"
        };
        jobbyClient.EnqueueCommand(jobParam);
    }

    private static void CreateRecurrent(IJobbyClient jobbyClient)
    {
        jobbyClient.ScheduleRecurrent(new TestCliRecurrentJobCommand(), "*/3 * * * * *");
    }

    private static void CreateSequence(IJobbyClient jobbyClient, int jobsCount)
    {
        var sequenceBuilder = jobbyClient.Factory.CreateSequenceBuilder();
        for (int i = 1; i <= jobsCount; i++)
        {
            sequenceBuilder.Add(new TestCliJobCommand { Id = i, Name = $"Job in sequence {i}", ShouldBeFailed = false });
        }
        jobbyClient.EnqueueBatch(sequenceBuilder.GetJobs());
    }

    private static void CancelRecurrent(IJobbyClient client)
    {
        client.CancelRecurrent<TestCliRecurrentJobCommand>();
    }
}
