using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services.Builders;
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
            DeleteCompleted = true,
            CompleteWithBatching = true
        };
        var scopeFactory = new TestJobExecutionScopeFactory();
        var defaultRetryPolicy = new RetryPolicy
        {
            MaxCount = 3,
            IntervalsSeconds = [1]
        };

        var builder = new JobbyServicesBuilder();
        builder
            .UsePostgresql(dataSource)
            .UseServerSettings(jobbySettings)
            .UseSystemTextJson(jsonOptions)
            .UseExecutionScopeFactory(scopeFactory)
            .UseRetryPolicy(x => x.UseByDefault(defaultRetryPolicy))
            .UseJobs(x => x
                .AddCommand<TestJobParam, TestJobHandler>()
                .AddRecurrentJob<TestRecurrentJobHandler>())
            .UseLoggerFactory(loggerFactory);

        var jobbyServer = builder.CreateJobbyServer();
        var jobsClient = builder.CreateJobsClient();
        var recurrentJobsClient = builder.CreateRecurrentJobsClient();

        Console.WriteLine("1. Demo success jobs");
        Console.WriteLine("2. Demo failed job");
        Console.WriteLine("3. Demo recurrent job");
        Console.WriteLine("4. Demo jobs sequence");

        string action = Console.ReadLine();

        switch (action)
        {
            case "1":
                CreateSuccess(jobsClient, 500);
                break;
            case "2":
                CreateFailed(jobsClient);
                break;
            case "3":
                CreateRecurrent(recurrentJobsClient);
                break;
            case "4":
                CreateSequence(jobsClient, 5);
                break;
        }

        jobbyServer.StartBackgroundService();

        Console.ReadLine();
        jobbyServer.SendStopSignal();
    }

    private static void CreateSuccess(IJobsClient client, int count)
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

    private static void CreateFailed(IJobsClient client)
    {
        var jobParam = new TestJobParam
        {
            Id = 500,
            ShouldBeFailed = true,
            Name = "SomeValue"
        };
        client.EnqueueCommand(jobParam);
    }

    private static void CreateRecurrent(IRecurrentJobsClient recurrentJobsClient)
    {
        recurrentJobsClient.ScheduleRecurrent<TestRecurrentJobHandler>("*/3 * * * * *");
    }

    private static void CreateSequence(IJobsClient client, int jobsCount)
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
            if (type == typeof(TestRecurrentJobHandler))
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
    }

    private class TestJobHandler : IJobCommandHandler<TestJobParam>
    {
        public Task ExecuteAsync(TestJobParam command, CommandExecutionContext ctx)
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

    private class TestRecurrentJobHandler : IRecurrentJobHandler
    {
        public static string GetRecurrentJobName()
        {
            return "TestRecurrentJob";
        }

        public Task ExecuteAsync(RecurrentJobExecutionContext ctx)
        {
            Console.WriteLine($"Recurrent Job {ctx.JobName}, {DateTime.Now}");
            return Task.CompletedTask;
        }
    }
}
