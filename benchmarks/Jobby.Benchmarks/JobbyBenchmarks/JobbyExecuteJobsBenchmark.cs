using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services.Builders;
using Jobby.Postgres.ConfigurationExtensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Diagnostics;

namespace Jobby.Benchmarks.JobbyBenchmarks;

public class JobbyExecuteJobsBenchmark : IBenchmark
{
    private readonly bool _useBenchmarkLib;

    public JobbyExecuteJobsBenchmark(bool useBenchmarkLib)
    {
        _useBenchmarkLib = useBenchmarkLib;
    }

    public string Name => _useBenchmarkLib 
        ? "Jobby.Execute" 
        : "Jobby.Execute.WithoutBenchmarkLib";

    public async Task Run()
    {
        if (_useBenchmarkLib)
        {
            BenchmarkRunner.Run<JobbyExecuteJobsBenchmarkAction>();
        }
        else
        {
            var action = new JobbyExecuteJobsBenchmarkAction();
            var benchmarkParams = BenchmarksHelper.GetJobbyParams();
            action.JobsCount = benchmarkParams.JobsCount;
            action.DegreeOfParallelism = benchmarkParams.DegreeOfParallelism;
            action.CompleteWithBatching = benchmarkParams.CompleteWithBatching;

            Console.WriteLine("Warmup...");
            action.Setup();
            action.JobbyExecuteJobs();

            Console.WriteLine("Setup...");
            action.Setup();

            Console.WriteLine("Pause before run...");
            await Task.Delay(3000);
            Console.WriteLine("Run!");

            var sw = new Stopwatch();
            sw.Start();
            action.JobbyExecuteJobs();
            sw.Stop();

            Console.WriteLine($"Jobs execution time: {sw.ElapsedMilliseconds} ms");
        }
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

    [Params(10, 30)]
    public int DegreeOfParallelism { get; set; } = 10;

    [Params(false, true)]
    public bool CompleteWithBatching { get; set; } = false;

    public int JobsCount { get; set; } = 1000;

    [IterationSetup]
    public void Setup()
    {
        var loggerFactory = LoggerFactory.Create(x => x.AddConsole());

        var serverSettings = new JobbyServerSettings
        {
            MaxDegreeOfParallelism = DegreeOfParallelism,
            TakeToProcessingBatchSize = 10,
            PollingIntervalMs = 1000,
            DbErrorPauseMs = 5000,
            DeleteCompleted = true,
            CompleteWithBatching = CompleteWithBatching,
        };
        var scopeFactory = new JobbyTestExecutionScopeFactory();

        var builder = new JobbyServicesBuilder();
        builder
            .UsePostgresql(_dataSource)
            .UseServerSettings(serverSettings)
            .UseExecutionScopeFactory(scopeFactory)
            .UseJobs(x => x.AddJob<JobbyTestJobCommand, JobbyTestJobCommandHandler>());

        _jobbyServer = builder.CreateJobbyServer();
        _jobsClient = builder.CreateJobsClient();

        Counter.Reset(JobsCount);

        JobbyHelper.RemoveAllJobs(_dataSource);
        var jobs = new List<Job>(capacity: JobsCount);
        for (int i = 1; i <= JobsCount; i++)
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