using Jobby.Abstractions.Models;
using Jobby.Abstractions.Server;
using Jobby.Core.Client;
using Jobby.Core.Server;
using Jobby.Postgres.CommonServices;
using Npgsql;
using System.Text.Json;

namespace Jobby.Samples.CliJobsSample;

internal class Program
{
    static async Task Main(string[] args)
    {
        var connectionString = "Host=localhost;Username=test_user;Password=12345;Database=test_db";
        using var dataSource = NpgsqlDataSource.Create(connectionString);
        var pgJobsStorage = new PgJobsStorage(dataSource);
        var jobsClient = new JobsClient(pgJobsStorage);
        var jobbySettings = new JobbySettings
        {
            PollingIntervalMs = 1000,
            DbErrorPauseMs = 5000,
            MaxDegreeOfParallelism = 10,
        };
        var scopeFactory = new TestJobExecutionScopeFactory();
        var jobsProcessor = new JobProcessor(scopeFactory, pgJobsStorage, jobbySettings);
        var pollingService = new JobsPollingService(pgJobsStorage, jobsProcessor, jobbySettings);
        pollingService.StartBackgroundService();

        for (int i = 1; i <= 20000; i++)
        {
            var jobParam = new TestJobParam { Id = i, Name = "SomeValue" };
            var job = new JobModel
            {
                JobName = "TestJob",
                JobParam = JsonSerializer.Serialize(jobParam),
            };
            await jobsClient.EnqueueAsync(job);
        }

        Console.ReadLine();
        pollingService.SendStopSignal();
        await Task.Delay(2000);
    }

    private class TestJobExecutor : IJobExecutor
    {
        public async Task ExecuteAsync(JobModel job)
        {
            var param = JsonSerializer.Deserialize<TestJobParam>(job.JobParam);
            await Task.Delay(50);
            //return Task.CompletedTask;
        }
    }

    private class TestJobExecutionScope : IJobExecutionScope
    {
        private readonly IJobExecutor _executor;

        public TestJobExecutionScope(IJobExecutor executor)
        {
            _executor = executor;
        }

        public void Dispose()
        {
        }

        public IJobExecutor GetJobExecutor(string jobName)
        {
            return _executor;
        }
    }

    private class TestJobExecutionScopeFactory : IJobExecutionScopeFactory
    {
        private readonly IJobExecutor _executor = new TestJobExecutor();

        public IJobExecutionScope CreateJobExecutionScope()
        {
            return new TestJobExecutionScope(_executor);
        }
    }

    private class TestJobParam
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
