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
        var jobbySettings = new JobbySettings
        {
            PollingIntervalMs = 1000,
            DbErrorPauseMs = 5000,
            MaxDegreeOfParallelism = 10,
            UseBatches = true,
        };
        var scopeFactory = new TestJobExecutionScopeFactory(serializer);
        var jobsServer = new JobsServer(pgJobsStorage, scopeFactory, jobbySettings);

        for (int i = 1; i <= 5; i++)
        {
            var jobParam = new TestJobParam { Id = i, Name = "SomeValue" };
            await jobsClient.EnqueueCommandAsync(jobParam);
        }

        jobsServer.StartBackgroundService();

        Console.ReadLine();
        jobsServer.SendStopSignal();
    }

    private class TestJobExecutionScope : JobExecutionScopeBase
    {
        public TestJobExecutionScope(IReadOnlyDictionary<string, Type> jobCommandTypesByName,
            IReadOnlyDictionary<Type, Type> handlerTypesByCommandType,
            IJobParamSerializer serializer) : base(jobCommandTypesByName, handlerTypesByCommandType, serializer)
        {
        }

        public override void Dispose()
        {
           
        }

        protected override object? CreateService(Type t)
        {
            if (t == typeof(IJobCommandHandler<TestJobParam>))
            {
                return new TestJobHandler();
            }
            return null;
        }
    }

    private class TestJobExecutionScopeFactory : IJobExecutionScopeFactory
    {
        private readonly IJobParamSerializer _serializer;
        private readonly Dictionary<string, Type> _jobCommandTypesByName;
        private readonly Dictionary<Type, Type> _jobHandlerTypesByCommandTypes;

        public TestJobExecutionScopeFactory(IJobParamSerializer serializer)
        {
            _serializer = serializer;
            _jobCommandTypesByName = new Dictionary<string, Type>()
            {
                ["TestJob"] = typeof(TestJobParam)
            };
            _jobHandlerTypesByCommandTypes = new Dictionary<Type, Type>()
            {
                [typeof(TestJobParam)] = typeof(IJobCommandHandler<TestJobParam>)
            };
        }

        public IJobExecutionScope CreateJobExecutionScope()
        {
            return new TestJobExecutionScope(_jobCommandTypesByName, _jobHandlerTypesByCommandTypes, _serializer);
        }
    }

    private class TestJobParam : IJobCommand
    {
        public int Id { get; set; }
        public string? Name { get; set; }

        public static string GetJobName() => "TestJob";
    }

    private class TestJobHandler : IJobCommandHandler<TestJobParam>
    {
        public Task ExecuteAsync(TestJobParam command)
        {
            Console.WriteLine($"Executed, Id = {command.Id}");
            return Task.CompletedTask;
        }
    }
}
