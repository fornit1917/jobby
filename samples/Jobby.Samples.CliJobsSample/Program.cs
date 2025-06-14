using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.Postgres.ConfigurationExtensions;
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
            DbErrorPauseMs = 5000,
            MaxDegreeOfParallelism = 10,
            TakeToProcessingBatchSize = 10,
            DeleteCompleted = false,
            CompleteWithBatching = true,
            HeartbeatIntervalSeconds = 3,
            MaxNoHeartbeatIntervalSeconds = 10
        };
        var scopeFactory = new TestJobExecutionScopeFactory();
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
            .AddJobsFromAssemblies(typeof(TestJobParam).Assembly);

        var jobbyServer = builder.CreateJobbyServer();
        var jobbyClient = builder.CreateJobbyClient();

        jobbyServer.StartBackgroundService();

        Console.WriteLine("1. Demo success jobs");
        Console.WriteLine("2. Demo failed job");
        Console.WriteLine("3. Demo recurrent job");
        Console.WriteLine("4. Demo jobs sequence");

        string action = Console.ReadLine();

        switch (action)
        {
            case "1":
                CreateSuccess(jobbyClient, 5);
                break;
            case "2":
                CreateFailed(jobbyClient);
                break;
            case "3":
                CreateRecurrent(jobbyClient);
                break;
            case "4":
                CreateSequence(jobbyClient, 5);
                break;
        }
        Console.ReadLine();
        jobbyServer.SendStopSignal();
    }

    private static void CreateSuccess(IJobbyClient client, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            var jobParam = new TestJobParam
            {
                Id = i,
                ShouldBeFailed = false,
                Name = "SomeValue"
            };
            client.EnqueueCommand(jobParam);
        }
    }

    private static void CreateFailed(IJobbyClient client)
    {
        var jobParam = new TestJobParam
        {
            Id = 500,
            ShouldBeFailed = true,
            Name = "SomeValue"
        };
        client.EnqueueCommand(jobParam);
    }

    private static void CreateRecurrent(IJobbyClient jobbyClient)
    {
        jobbyClient.ScheduleRecurrent(new TestRecurrentJobCommand(), "*/3 * * * * *");
    }

    private static void CreateSequence(IJobbyClient client, int jobsCount)
    {
        var builder = client.Factory.CreateSequenceBuilder();
        for (int i = 1; i <= jobsCount; i++)
        {
            builder.Add(new TestJobParam { Id = i, Name = $"Job in sequence {i}", ShouldBeFailed = false });
        }
        client.EnqueueBatch(builder.GetJobs());
    }

    private class TestJobExecutionScope : IJobExecutionScope
    {
        public void Dispose()
        {
        }

        public object? GetService(Type type)
        {
            if (type == typeof(IJobCommandHandler<TestJobParam>))
            {
                return new TestJobHandler();
            }
            if (type == typeof(IJobCommandHandler<TestRecurrentJobCommand>))
            {
                return new TestRecurrentJobHandler();
            }
            return null;
        }
    }

    private class TestJobExecutionScopeFactory : IJobExecutionScopeFactory
    {
        public IJobExecutionScope CreateJobExecutionScope()
        {
            return new TestJobExecutionScope();
        }
    }

    private class TestJobParam : IJobCommand
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public bool ShouldBeFailed { get; set; }

        public static string GetJobName() => "TestJob";

        public bool CanBeRestarted() => Id % 2 == 0;
    }

    private class TestJobHandler : IJobCommandHandler<TestJobParam>
    {
        public Task ExecuteAsync(TestJobParam command, JobExecutionContext ctx)
        {
            if (command.ShouldBeFailed)
            {
                Console.WriteLine($"Exception will be thrown, Id = {command.Id}");
                throw new Exception("Error message");
            }

            Console.WriteLine($"Executed, Id = {command.Id}");
            return Task.CompletedTask;
        }
    }

    private class TestRecurrentJobCommand : IJobCommand
    {
        public static string GetJobName()
        {
            return "TestRecurrentJob";
        }

        public bool CanBeRestarted() => false;
    }

    private class TestRecurrentJobHandler : IJobCommandHandler<TestRecurrentJobCommand>
    {
        public static string GetRecurrentJobName()
        {
            return "TestRecurrentJob";
        }

        public Task ExecuteAsync(TestRecurrentJobCommand command, JobExecutionContext ctx)
        {
            Console.WriteLine($"Recurrent Job {ctx.JobName}, {DateTime.Now}");
            return Task.CompletedTask;
        }
    }
}
