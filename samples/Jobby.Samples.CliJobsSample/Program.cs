using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.Postgres.ConfigurationExtensions;
using Jobby.Samples.CliJobsSample.HandlerFactory;
using Jobby.Samples.CliJobsSample.Jobs;
using Jobby.Samples.CliJobsSample.Middlewares;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.Json;

namespace Jobby.Samples.CliJobsSample;

internal static class Program
{
    static void Main()
    {
        Console.Write("Queues count (default 1): ");
        var queuesCountAnswer = Console.ReadLine();
        var queuesCount = string.IsNullOrEmpty(queuesCountAnswer) ? 1 : int.Parse(queuesCountAnswer);
        var queues = Enumerable.Range(1, queuesCount)
            .Select(i => $"q{i}")
            .Select(q => new QueueSettings { QueueName = q })
            .ToList();
        
        var connectionString = "Host=localhost;Username=test_user;Password=12345;Database=test_db;GSS Encryption Mode=Disable";
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
            MaxNoHeartbeatIntervalSeconds = 10,
            Queues = queues
        };
        var scopeFactory = new SimpleJobExecutionScopeFactory();
        var defaultRetryPolicy = new RetryPolicy
        {
            MaxCount = 3,
            IntervalsSeconds = [1],
            JitterMaxValuesMs = [500]
        };

        var builder = new JobbyBuilder();
        builder.AddJobsFromAssemblies(typeof(TestCliJobCommand).Assembly);
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
            .ConfigurePipeline(pipeline =>
            {
                pipeline.Use(new DemoCliMiddleware());
            });
        
        builder.CreateStorageMigrator().Migrate();
        var jobbyServer = builder.CreateJobbyServer();
        var jobbyClient = builder.CreateJobbyClient();

        Console.WriteLine("1. Enqueue success jobs");
        Console.WriteLine("2. Enqueue failed job");
        Console.WriteLine("3. Enqueue jobs sequence");
        Console.WriteLine("4. Schedule recurrent exclusive job");
        Console.WriteLine("5. Cancel recurrent exclusive job");
        Console.WriteLine("6. Enqueue serializable jobs group");
        Console.WriteLine("7. Schedule recurrent not exclusive jobs");

        string? action = Console.ReadLine();

        switch (action)
        {
            case "1":
                Console.Write("Jobs count (default 5): ");
                var jobsCountAnswer = Console.ReadLine();
                int jobsCount = string.IsNullOrEmpty(jobsCountAnswer) ? 5 : int.Parse(jobsCountAnswer);
                CreateSuccess(jobbyClient, jobsCount, queuesCount);
                break;
            case "2":
                CreateFailed(jobbyClient);
                break;
            case "3":
                CreateSequence(jobbyClient, 5, queuesCount);
                break;
            case "4":
                CreateRecurrent(jobbyClient);
                break;
            case "5":
                CancelRecurrent(jobbyClient);
                break;
            case "6":
                CreateSerializableJobsGroup(jobbyClient, 5, queuesCount);
                break;
            case "7":
                CreateRecurrentNotExclusive(jobbyClient);
                break;
        }


        Console.Write("Start server? (y/n): ");
        var runServer = Console.ReadLine();
        if (runServer != "n")
            jobbyServer.StartBackgroundService();

        Console.ReadLine();

        jobbyServer.SendStopSignal();
    }

    private static void CreateSuccess(IJobbyClient jobbtClient, int jobsCount, int queuesCount)
    {
        for (int i = 1; i <= jobsCount; i++)
        {
            var jobParam = new TestCliJobCommand
            {
                Id = i,
                ShouldBeFailed = false,
                Name = "SomeValue"
            };
            var queueName = $"q{(i - 1) % queuesCount + 1}";
            jobbtClient.EnqueueCommand(jobParam, new JobOpts {  QueueName = queueName });
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
        jobbyClient.EnqueueCommand(jobParam, new JobOpts { QueueName = "q1" });
    }

    private static void CreateRecurrent(IJobbyClient jobbyClient)
    {
        jobbyClient.ScheduleRecurrent(new TestCliRecurrentJobCommand(), "*/3 * * * * *", new RecurrentJobOpts
        {
            QueueName = "q1",
        });
    }

    private static void CreateSequence(IJobbyClient jobbyClient, int jobsCount, int queuesCount)
    {
        var sequenceBuilder = jobbyClient.Factory.CreateSequenceBuilder();
        for (int i = 1; i <= jobsCount; i++)
        {
            var queueName = $"q{(i - 1) % queuesCount + 1}";
            var command = new TestCliJobCommand { Id = i, Name = $"Job in sequence {i}", ShouldBeFailed = false }; 
            sequenceBuilder.Add(command, new JobOpts { QueueName = queueName });
        }
        jobbyClient.EnqueueBatch(sequenceBuilder.GetJobs());
    }

    private static void CreateSerializableJobsGroup(IJobbyClient jobbyClient, int jobsCount, int queuesCount)
    {
        for (int i = 1; i <= jobsCount; i++)
        {
            var queueName = $"q{(i - 1) % queuesCount + 1}";
            var command = new TestCliJobCommand { Id = i, Name = $"Job in sequence {i}", ShouldBeFailed = false }; 
            jobbyClient.EnqueueCommand(command, new JobOpts
            {
                QueueName = queueName,
                SerializableGroupId = "gid"
            });
        }
    }

    private static void CreateRecurrentNotExclusive(IJobbyClient jobbyClient)
    {
        jobbyClient.ScheduleRecurrent(new TestCliRecurrentJobCommand { Value = "1" }, "*/2 * * * * *", new()
        {
            QueueName = "q1",
            IsExclusive = false
        });
        
        jobbyClient.ScheduleRecurrent(new TestCliRecurrentJobCommand() { Value = "2" }, "*/3 * * * * *", new()
        {
            QueueName = "q1",
            IsExclusive = false
        });
    }

    private static void CancelRecurrent(IJobbyClient client)
    {
        client.CancelRecurrent<TestCliRecurrentJobCommand>();
    }
}
