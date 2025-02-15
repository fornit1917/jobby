using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Quartz;

namespace Jobby.Samples.Benchmarks.QuartzBenchmarks;

public class QuartzCreateJobsBenchmark : IBenchmark
{
    public string Name => "Quartz.Create.5";

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
        const int jobsCount = 5;
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
}