
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Quartz;

namespace Jobby.Benchmarks.QuartzBenchmarks;

public class QuartzBulkCreateJobsBenchmark : IBenchmark
{
    public string Name => "Quartz.BulkCreate.10";

    public Task Run()
    {
        BenchmarkRunner.Run<QuartzBulkCreateJobsBenchmarkAction>();
        return Task.CompletedTask;
    }
}

[MemoryDiagnoser]
[WarmupCount(10)]
[IterationCount(10)]
public class QuartzBulkCreateJobsBenchmarkAction
{
    private readonly IScheduler _scheduler;

    public QuartzBulkCreateJobsBenchmarkAction()
    {
        _scheduler = QuartzHelper.CreateScheduler().GetAwaiter().GetResult();
    }

    [Benchmark]
    public void QuartzBulkCreateJobs()
    {
        const int jobsCount = 10;
        var jobParams = new List<QuartzTestJobParam>(capacity: jobsCount);
        for (int i = 0; i < jobsCount; i++)
        {
            var jobParam = new QuartzTestJobParam
            {
                Id = i,
                Value = Guid.NewGuid().ToString(),
                DelayMs = 0,
            };
            jobParams.Add(jobParam);
        }
        QuartzHelper.CreateTestJobs(_scheduler, jobParams).GetAwaiter().GetResult();
    }
}