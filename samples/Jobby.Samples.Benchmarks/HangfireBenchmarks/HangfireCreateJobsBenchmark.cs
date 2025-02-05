using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Hangfire;

namespace Jobby.Samples.Benchmarks.HangfireBenchmarks;

public class HangfireCreateJobsBenchmark : IBenchmark
{
    public string Name => "Hangfire.Create.5";

    public Task Run()
    {
        BenchmarkRunner.Run<HangfireCreateJobsBenchmarkAction>();
        return Task.CompletedTask;
    }
}

[MemoryDiagnoser]
public class HangfireCreateJobsBenchmarkAction
{
    public HangfireCreateJobsBenchmarkAction()
    {
        var dataSource = DataSourceFactory.Create();
        HangfireHelper.ConfigureGlobal(dataSource);
    }

    [Benchmark]
    public void HangfireCreateJobs()
    {
        const int jobsCount = 5;
        for (int i = 1; i <= jobsCount; i++)
        {
            var jobParam = new TestJobParam
            {
                Id = i,
                Value = Guid.NewGuid().ToString(),
            };
            BackgroundJob.Enqueue<TestJob>(x => x.Execute(jobParam));
        }
    }
}
