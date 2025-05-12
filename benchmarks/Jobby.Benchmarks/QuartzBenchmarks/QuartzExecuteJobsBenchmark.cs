using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Npgsql;
using Quartz;
using System.Diagnostics;

namespace Jobby.Benchmarks.QuartzBenchmarks;

public class QuartzExecuteJobsBenchmark : IBenchmark
{
    private readonly bool _useBenchamrkLib;

    public QuartzExecuteJobsBenchmark(bool useBenchamrkLib)
    {
        _useBenchamrkLib = useBenchamrkLib;
    }

    public string Name => _useBenchamrkLib ? "Quartz.Execute" : "Quartz.Execute.WithoutBenchmarkLib";

    public async Task Run()
    {
        if (_useBenchamrkLib)
        {
            BenchmarkRunner.Run<QuartzExecuteJobsBenchmarkAction>();
        }
        else
        {
            var benchmarkParams = BenchmarksHelper.GetCommonParams();
            var action = new QuartzExecuteJobsBenchmarkAction();
            action.JobsCount = benchmarkParams.JobsCount;
            action.DegreeOfParallelism = benchmarkParams.DegreeOfParallelism;

            Console.WriteLine("Warmup...");
            action.Setup();
            action.QuartzExecuteJobs();
            action.Cleanup();

            Console.WriteLine("Setup...");
            action.Setup();

            Console.WriteLine("Pause before run...");
            await Task.Delay(3000);
            Console.WriteLine("Run!");

            var sw = new Stopwatch();
            sw.Start();
            action.QuartzExecuteJobs();
            sw.Stop();
            action.Cleanup();

            Console.WriteLine($"Jobs execution time: {sw.ElapsedMilliseconds} ms");
        }   
    }
}

[MemoryDiagnoser]
[WarmupCount(1)]
[IterationCount(1)]
[ProcessCount(1)]
[InvocationCount(1)]
public class QuartzExecuteJobsBenchmarkAction
{
    private readonly NpgsqlDataSource _dataSource;
    private IScheduler? _scheduler;

    public int JobsCount { get; set; } = 1000;

    [Params(10, 30)]
    public int DegreeOfParallelism { get; set; } = 10;

    public QuartzExecuteJobsBenchmarkAction()
    {
        _dataSource = DataSourceFactory.Create();
    }

    [IterationSetup]
    public void Setup()
    {
        _scheduler = QuartzHelper.CreateScheduler(maxConcurrency: DegreeOfParallelism).GetAwaiter().GetResult();

        Counter.Reset(JobsCount);

        QuartzHelper.RemoveAllJobs(_dataSource);
        for (int i = 1; i <= JobsCount; i++)
        {
            var jobParam = new QuartzTestJobParam
            {
                Id = i,
                Value = Guid.NewGuid().ToString(),
                DelayMs = 0,
            };
            QuartzHelper.CreateTestJob(_scheduler, jobParam).GetAwaiter().GetResult();
        }
    }

    [IterationCleanup]
    public void Cleanup()
    {
        _scheduler?.Shutdown().GetAwaiter().GetResult();
    }

    [Benchmark]
    public void QuartzExecuteJobs()
    {
        _scheduler?.Start();
        Counter.Event.WaitOne();
    }
}