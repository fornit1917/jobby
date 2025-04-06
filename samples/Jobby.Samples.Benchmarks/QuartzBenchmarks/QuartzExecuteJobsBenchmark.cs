using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Npgsql;
using Quartz;
using System.Diagnostics;

namespace Jobby.Samples.Benchmarks.QuartzBenchmarks;

public class QuartzExecuteJobsBenchmark : IBenchmark
{
    public string Name => "Quartz.Execute";

    public Task Run()
    {
        BenchmarkRunner.Run<QuartzExecuteJobsBenchmarkAction>();
        return Task.CompletedTask;
    }
}

[MemoryDiagnoser]
[WarmupCount(2)]
[IterationCount(2)]
[ProcessCount(1)]
[InvocationCount(1)]
public class QuartzExecuteJobsBenchmarkAction
{
    private readonly NpgsqlDataSource _dataSource;
    private IScheduler _scheduler;

    public QuartzExecuteJobsBenchmarkAction()
    {
        _dataSource = DataSourceFactory.Create();
    }

    [IterationSetup]
    public void Setup()
    {
        _scheduler = QuartzHelper.CreateScheduler().GetAwaiter().GetResult();

        const int jobsCount = 1000;

        Counter.Reset(jobsCount);

        QuartzHelper.RemoveAllJobs(_dataSource);
        for (int i = 1; i <= jobsCount; i++)
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
        _scheduler.Shutdown().GetAwaiter().GetResult();
    }

    [Benchmark]
    public void Run()
    {
        _scheduler.Start();
        Counter.Event.WaitOne();
    }
}