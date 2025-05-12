using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
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
    private NpgsqlDataSource _dataSource;
    private IJobbyServer? _jobbyServer;
    private IJobsClient? _jobsClient;

    public JobbyExecuteJobsBenchmarkAction()
    {
        _dataSource = DataSourceFactory.Create();
    }

    [IterationSetup]
    public void Setup()
    {
        var loggerFactory = LoggerFactory.Create(x => x.AddConsole());

        var serverSettings = new JobbyServerSettings
        {
            MaxDegreeOfParallelism = 30,
            TakeToProcessingBatchSize = 10,
            PollingIntervalMs = 1000,
            DbErrorPauseMs = 5000,
            DeleteCompleted = true,
            CompleteWithBatching = false,
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

        const int jobsCount = 1000;
        Counter.Reset(jobsCount);

        JobbyHelper.RemoveAllJobs(_dataSource);
        var jobs = new List<Job>(capacity: jobsCount);
        for (int i = 1; i <= jobsCount; i++)
        {
            var jobCommand = new JobbyTestJobCommand
            {
                Id = i,
                Value = Guid.NewGuid().ToString(),
                DelayMs = 0,
            };
            var job = _jobsClient.Factory.Create(jobCommand);
            jobs.Add(job);
        }
        _jobsClient.EnqueueBatch(jobs);
    }


    [Benchmark]
    public void JobbyExecuteJobs()
    {
        _jobbyServer?.StartBackgroundService();
        Counter.Event.WaitOne();
        _jobbyServer?.SendStopSignal();
    }
}