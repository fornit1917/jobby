using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.Postgres.ConfigurationExtensions;
using Npgsql;

namespace Jobby.Benchmarks.JobbyBenchmarks;

public class JobbyMultiQueueExecuteJobsBenchmark : IBenchmark
{
    private readonly bool _useBenchmarkLib;

    public JobbyMultiQueueExecuteJobsBenchmark(bool useBenchmarkLib)
    {
        _useBenchmarkLib = useBenchmarkLib;
    }

    public string Name => _useBenchmarkLib 
        ? "Jobby.MQ.Execute" 
        : "Jobby.MQ.Execute.WithoutBenchmarkLib";
    
    public async Task Run()
    {
        if (_useBenchmarkLib)
        {
            BenchmarkRunner.Run<JobbyMultiQueueExecuteJobsBenchmarkAction>();
        }
        else
        {
            var action = new JobbyMultiQueueExecuteJobsBenchmarkAction();
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
public class JobbyMultiQueueExecuteJobsBenchmarkAction
{
    private readonly NpgsqlDataSource _dataSource = DataSourceFactory.Create();
    private IJobbyServer? _jobbyServer;
    private IJobbyClient? _jobbyClient;
    
    [Params(10, 30)]
    public int DegreeOfParallelism { get; set; } = 10;

    [Params(false, true)]
    public bool CompleteWithBatching { get; set; } = false;
    
    public int JobsCount { get; set; } = 1000;
    
    [IterationSetup]
    public void Setup()
    {
        var serverSettings = new JobbyServerSettings
        {
            MaxDegreeOfParallelism = DegreeOfParallelism,
            TakeToProcessingBatchSize = DegreeOfParallelism,
            PollingIntervalMs = 1000,
            DbErrorPauseMs = 5000,
            DeleteCompleted = true,
            CompleteWithBatching = CompleteWithBatching,
            Queues = [
                new QueueSettings { QueueName = "q1" },
                new QueueSettings { QueueName = "q2" },
                new QueueSettings { QueueName = "q3" },
                new QueueSettings { QueueName = "q4" },
                new QueueSettings { QueueName = "q5" },
            ]
        };
        var scopeFactory = new JobbyTestExecutionScopeFactory();

        var builder = new JobbyBuilder();
        builder.AddJob<JobbyTestJobCommand, JobbyTestJobCommandHandler>();
        builder
            .UsePostgresql(_dataSource)
            .UseServerSettings(serverSettings)
            .UseExecutionScopeFactory(scopeFactory);

        var jobbyStorageMigrator = builder.CreateStorageMigrator();
        jobbyStorageMigrator.Migrate();
        
        _jobbyServer = builder.CreateJobbyServer();
        _jobbyClient = builder.CreateJobbyClient();

        Counter.Reset(JobsCount);

        JobbyHelper.RemoveAllJobs(_dataSource);
        var jobs = new List<JobCreationModel>(capacity: JobsCount);
        for (int i = 1; i <= JobsCount; i++)
        {
            var queueName = $"q{(i - 1) % serverSettings.Queues.Count + 1}";
            var jobCommand = new JobbyTestJobCommand
            {
                Id = i,
                Value = Guid.NewGuid().ToString(),
                DelayMs = 0,
            };
            var job = _jobbyClient.Factory.Create(jobCommand, new JobOpts {  QueueName = queueName });
            jobs.Add(job);
            if (jobs.Count == 1000)
            {
                _jobbyClient.EnqueueBatch(jobs);
                jobs.Clear();
            }
        }
        _jobbyClient.EnqueueBatch(jobs);
    }

    [Benchmark]
    public void JobbyExecuteJobs()
    {
        _jobbyServer?.StartBackgroundService();
        Counter.Event.WaitOne();
        _jobbyServer?.SendStopSignal();
    }
}