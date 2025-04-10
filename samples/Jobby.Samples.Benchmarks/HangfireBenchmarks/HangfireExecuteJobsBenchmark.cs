using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Hangfire;
using Npgsql;
using System.Diagnostics;

namespace Jobby.Samples.Benchmarks.HangfireBenchmarks;

public class HangfireExecuteJobsBenchmark : IBenchmark
{
    public string Name => "Hangfire.Execute";

    public Task Run()
    {
        BenchmarkRunner.Run<HangfireExecuteJobsBenchmarkAction>();
        return Task.CompletedTask;
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

    public HangfireExecuteJobsBenchmarkAction()
    {
        _dataSource = DataSourceFactory.Create();
    }

    [IterationSetup]
    public void Setup()
    {
        const int jobsCount = 1000;
        Counter.Reset(jobsCount);

        HangfireHelper.DropHangfireTables(_dataSource);
        HangfireHelper.ConfigureGlobal(_dataSource);

        for (int i = 1; i <= jobsCount; i++)
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
            WorkerCount = 10
        });

        Counter.Event.WaitOne();
    }
}
