using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.Core.Services.Builders;
using Jobby.Postgres.ConfigurationExtensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Diagnostics;

namespace Jobby.Benchmarks.JobbyBenchmarks;

public class JobbyExecuteJobsBenchmark : IBenchmark
{
    public string Name => "Jobby.Execute";

    public Task Run()
    {
        BenchmarkRunner.Run<JobbyExecuteJobsBenchmarkAction>();
        return Task.CompletedTask;
    }
}

[MemoryDiagnoser]
[WarmupCount(1)]
[IterationCount(1)]
[ProcessCount(1)]
[InvocationCount(1)]
public class JobbyExecuteJobsBenchmarkAction
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IJobbyServer _jobbyServer;
    private readonly IJobsClient _jobsClient;

    public JobbyExecuteJobsBenchmarkAction()
    {
        var loggerFactory = LoggerFactory.Create(x => x.AddConsole());
        _dataSource = DataSourceFactory.Create();
        var serverSettings = new JobbyServerSettings
        {
            MaxDegreeOfParallelism = 10,
            TakeToProcessingBatchSize = 10,
            PollingIntervalMs = 1000,
            DbErrorPauseMs = 5000,
            DeleteCompleted = true,
            CompleteWithBatching = true,
        };
        var scopeFactory = new JobbyTestExecutionScopeFactory();

        var builder = new JobbyServicesBuilder();
        builder
            .UsePostgresql(_dataSource)
            .UseServerSettings(serverSettings)
            .UseExecutionScopeFactory(scopeFactory)
            .UseJobs(x => x.AddCommand<JobbyTestJobCommand, JobbyTestJobCommandHandler>());

        _jobbyServer = builder.CreateJobbyServer();
        _jobsClient = builder.CreateJobsClient();
    }

    [IterationSetup]
    public void Setup()
    {
        const int jobsCount = 1000;
        Counter.Reset(jobsCount);

        _jobbyServer.SendStopSignal();
        JobbyHelper.RemoveAllJobs(_dataSource);
        for (int i = 1; i <= jobsCount; i++)
        {
            var jobCommand = new JobbyTestJobCommand
            {
                Id = i,
                Value = Guid.NewGuid().ToString(),
                DelayMs = 0,
            };
            _jobsClient.EnqueueCommand(jobCommand);
        }
    }

    [Benchmark]
    public void JobbyExecuteJobs()
    {
        _jobbyServer.StartBackgroundService();
        Counter.Event.WaitOne();
    }
}