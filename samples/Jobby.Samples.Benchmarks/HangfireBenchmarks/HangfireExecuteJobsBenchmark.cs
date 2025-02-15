using Hangfire;
using System.Diagnostics;

namespace Jobby.Samples.Benchmarks.HangfireBenchmarks;

public class HangfireExecuteJobsBenchmark : IBenchmark
{
    public string Name => "Hangfire.Execute";

    public async Task Run()
    {
        var benchmarkParams = BenchamrkHelper.GetJobsBenchmarkParams(defaultJobsCount: 1000, defaultJobDelayMs: 0);

        var dataSource = DataSourceFactory.Create();

        Console.WriteLine("Drop old hangfire data");
        HangfireHelper.DropHangfireTables(dataSource);

        Console.WriteLine("Configure hangfire");
        HangfireHelper.ConfigureGlobal(dataSource);

        Console.WriteLine($"Create {benchmarkParams.JobsCount} jobs");
        for (int i = 1; i <= benchmarkParams.JobsCount; i++)
        {
            var jobParam = new HangfireTestJobParam
            {
                Id = i,
                DelayMs = benchmarkParams.JobDelayMs,
                Value = Guid.NewGuid().ToString(),
            };
            BackgroundJob.Enqueue<HangfireTestJob>(x => x.Execute(jobParam));
        }

        Console.WriteLine("Start hangfire server");
        Stopwatch stopwatch = Stopwatch.StartNew();
        using var server = new BackgroundJobServer(new BackgroundJobServerOptions
        {
            SchedulePollingInterval = TimeSpan.FromSeconds(1),
            WorkerCount = 10
        });
        var hasNotCompletedJobs = true;
        while (hasNotCompletedJobs)
        {
            hasNotCompletedJobs = HangfireHelper.HasNotCompletedJobs(dataSource);
            if (hasNotCompletedJobs)
            {
                await Task.Delay(50);
            }
        }
        stopwatch.Stop();

        Console.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds} ms");
    }
}
