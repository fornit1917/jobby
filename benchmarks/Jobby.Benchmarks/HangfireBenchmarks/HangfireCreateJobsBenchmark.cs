using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Hangfire;

namespace Jobby.Benchmarks.HangfireBenchmarks;

public class HangfireCreateJobsBenchmark : IBenchmark
{
    public string Name => "Hangfire.Create.1";

    public Task Run()
    {
        BenchmarkRunner.Run<HangfireCreateJobsBenchmarkAction>();
        return Task.CompletedTask;
    }
}

[MemoryDiagnoser]
[WarmupCount(10)]
[IterationCount(10)]
public class HangfireCreateJobsBenchmarkAction
{
    public HangfireCreateJobsBenchmarkAction()
    {
        var dataSource = DataSourceFactory.Create();
        HangfireHelper.ConfigureGlobal(dataSource);
    }

    [Benchmark]
    public void HangfireCreateJob()
    {
        var jobParam = new HangfireTestJobParam
        {
            Id = 1,
            Value = Guid.NewGuid().ToString(),
        };
        BackgroundJob.Enqueue<HangfireTestJob>(x => x.Execute(jobParam));
    }
}
