using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.Postgres;
using Npgsql;
using System.Text.Json;

namespace Jobby.Samples.CliJobsSample;

internal class Program
{
    static async Task Main(string[] args)
    {
        var connectionString = "Host=localhost;Username=test_user;Password=12345;Database=test_db";
        using var dataSource = NpgsqlDataSource.Create(connectionString);

        var jsonOptions = new JsonSerializerOptions();
        var serializer = new SystemTextJsonJobParamSerializer(jsonOptions);

        var pgJobsStorage = new PgJobsStorage(dataSource);
        var jobsClient = new JobsClient(pgJobsStorage, serializer);
        var recurrentJobsClient = new RecurrentJobsClient(pgJobsStorage);
        var jobbySettings = new JobbySettings
        {
            PollingIntervalMs = 1000,
            DbErrorPauseMs = 5000,
            MaxDegreeOfParallelism = 10,
            UseBatches = true,
        };
        var scopeFactory = new TestJobExecutionScopeFactory();

        var defaultRetryPolicy = new RetryPolicy
        {
            MaxCount = 3,
            IntervalsSeconds = [1]
        };
        var retryPolicyService = new RetryPolicyService(defaultRetryPolicy);
        
        var jobsRegistry = new JobsRegistryBuilder()
            .AddJob<TestJobParam, TestJobHandler>()
            .AddRecurrentJob<TestRecurrentJobHandler>()
            .Build();

        var jobsServer = new JobsServer(pgJobsStorage, scopeFactory, retryPolicyService, jobsRegistry, serializer, jobbySettings);

        Console.WriteLine("1. Demo success jobs");
        Console.WriteLine("2. Demo failed job");
        Console.WriteLine("3. Demo recurrent job");

        string action = Console.ReadLine();

        switch (action)
        {
            case "1":
                CreateSuccess(jobsClient, 5);
                break;
            case "2":
                CreateFailed(jobsClient);
                break;
            case "3":
                CreateRecurrent(recurrentJobsClient);
                break;
        }

        jobsServer.StartBackgroundService();

        Console.ReadLine();
        jobsServer.SendStopSignal();
    }

    private static void CreateSuccess(IJobsMediator jobsMediator, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            var jobParam = new TestJobParam
            {
                Id = i,
                ShouldBeFailed = false,
                Name = "SomeValue"
            };
            jobsMediator.EnqueueCommand(jobParam);
        }
    }

    private static void CreateFailed(IJobsMediator jobsMediator)
    {
        var jobParam = new TestJobParam
        {
            Id = 500,
            ShouldBeFailed = true,
            Name = "SomeValue"
        };
        jobsMediator.EnqueueCommand(jobParam);
    }

    private static void CreateRecurrent(IRecurrentJobsClient recurrentJobsClient)
    {
        recurrentJobsClient.ScheduleRecurrent<TestRecurrentJobHandler>("*/3 * * * * *");
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
