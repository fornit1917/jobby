using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Hangfire;
using Npgsql;

namespace Jobby.Benchmarks.HangfireBenchmarks;

public class HangfireMultiQueueExecuteJobsBenchmark : IBenchmark
{
    private readonly bool _useBenchmarkLib;

    public HangfireMultiQueueExecuteJobsBenchmark(bool useBenchmarkLib)
    {
        _useBenchmarkLib = useBenchmarkLib;
    }

    public string Name => _useBenchmarkLib ? "Hangfire.MQ.Execute" : "Hangfire.MQ.Execute.WithoutBenchmarkLib";

    public async Task Run()
    {
        if (_useBenchmarkLib)
        {
            BenchmarkRunner.Run<HangfireMultiQueueExecuteJobsBenchmarkAction>();
        }
        else
        {
            var action = new HangfireMultiQueueExecuteJobsBenchmarkAction();
            var benchmarkParams = BenchmarksHelper.GetCommonParams();
            action.JobsCount = benchmarkParams.JobsCount;
            action.DegreeOfParallelism = benchmarkParams.DegreeOfParallelism;

            Console.WriteLine("Warmup...");
            action.Setup();
            action.HangfireExecuteJobs();

            Console.WriteLine("Setup...");
            action.Setup();

            Console.WriteLine("Pause before run...");
            await Task.Delay(3000);
            Console.WriteLine("Run!");

            var sw = new Stopwatch();
            sw.Start();
            action.HangfireExecuteJobs();
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
public class HangfireMultiQueueExecuteJobsBenchmarkAction
{
    private static readonly string[] Queues = ["q1", "q2", "q3", "q4", "q5"];
    
    private readonly NpgsqlDataSource _dataSource = DataSourceFactory.Create();

    public int JobsCount { get; set; } = 1000;

    [Params(10, 30)]
    public int DegreeOfParallelism { get; set; }

    [IterationSetup]
    public void Setup()
    {
        Counter.Reset(JobsCount);

        HangfireHelper.DropHangfireTables(_dataSource);
        HangfireHelper.ConfigureGlobal(_dataSource);

        for (int i = 1; i <= JobsCount; i++)
        {
            var queueName = $"q{(i - 1) % Queues.Length + 1}";
            var jobParam = new HangfireTestJobParam
            {
                Id = i,
                DelayMs = 0,
                Value = Guid.NewGuid().ToString(),
            };
            BackgroundJob.Enqueue<HangfireTestJob>(queueName, x => x.Execute(jobParam));
        }
    }

    [Benchmark]
    public void HangfireExecuteJobs()
    {
        using var server = new BackgroundJobServer(new BackgroundJobServerOptions
        {
            SchedulePollingInterval = TimeSpan.FromSeconds(1),
            WorkerCount = DegreeOfParallelism,
            Queues = Queues
        });

        Counter.Event.WaitOne();
    }
}