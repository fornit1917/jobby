using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Quartz;

namespace Jobby.Benchmarks.QuartzBenchmarks;

public class QuartzCreateJobsBenchmark : IBenchmark
{
    public string Name => "Quartz.Create.1";

    public Task Run()
    {
        BenchmarkRunner.Run<QuartzCreateJobsBenchmarkAction>();
        return Task.CompletedTask;
    }
}

[MemoryDiagnoser]
public class QuartzCreateJobsBenchmarkAction
{
    private readonly IScheduler _scheduler;

    public QuartzCreateJobsBenchmarkAction()
    {
        _scheduler = QuartzHelper.CreateScheduler().GetAwaiter().GetResult();
    }

    [Benchmark]
    public void QuartzCreateJobs()
    {
        var jobParam = new QuartzTestJobParam
        {
            Id = 1,
            Value = Guid.NewGuid().ToString(),
            DelayMs = 0,
        };
        QuartzHelper.CreateTestJob(_scheduler, jobParam).GetAwaiter().GetResult();
    }
}