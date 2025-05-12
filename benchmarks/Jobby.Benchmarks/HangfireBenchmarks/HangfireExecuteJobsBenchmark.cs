using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Hangfire;
using Npgsql;
using System.Diagnostics;

namespace Jobby.Benchmarks.HangfireBenchmarks;

public class HangfireExecuteJobsBenchmark : IBenchmark
{
    private readonly bool _useBenchmarkLib;

    public HangfireExecuteJobsBenchmark(bool useBenchmarkLib)
    {
        _useBenchmarkLib = useBenchmarkLib;
    }

    public string Name => _useBenchmarkLib ? "Hangfire.Execute" : "Hangfire.Execute.WithoutBenchmarkLib";

    public async Task Run()
    {
        if (_useBenchmarkLib)
        {
            BenchmarkRunner.Run<HangfireExecuteJobsBenchmarkAction>();
        }
        else
        {
            var action = new HangfireExecuteJobsBenchmarkAction();
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
public class HangfireExecuteJobsBenchmarkAction
{
    private readonly NpgsqlDataSource _dataSource;

    public int JobsCount { get; set; } = 1000;

    [Params(10, 30)]
    public int DegreeOfParallelism { get; set; }

    public HangfireExecuteJobsBenchmarkAction()
    {
        _dataSource = DataSourceFactory.Create();
    }

    [IterationSetup]
    public void Setup()
    {
        Counter.Reset(JobsCount);

        HangfireHelper.DropHangfireTables(_dataSource);
        HangfireHelper.ConfigureGlobal(_dataSource);

        for (int i = 1; i <= JobsCount; i++)
        {
            var jobParam = new HangfireTestJobParam
            {
                Id = i,
                DelayMs = 0,
                Value = Guid.NewGuid().ToString(),
            };
            BackgroundJob.Enqueue<HangfireTestJob>(x => x.Execute(jobParam));
        }
    }

    [Benchmark]
    public void HangfireExecuteJobs()
    {
        using var server = new BackgroundJobServer(new BackgroundJobServerOptions
        {
            SchedulePollingInterval = TimeSpan.FromSeconds(1),
            WorkerCount = DegreeOfParallelism,
        });

        Counter.Event.WaitOne();
    }
}
