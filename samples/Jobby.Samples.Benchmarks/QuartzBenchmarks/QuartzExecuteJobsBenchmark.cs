
using Jobby.Core.Server;
using Jobby.Samples.Benchmarks.JobbyBenchmarks;
using Quartz;
using System.Diagnostics;

namespace Jobby.Samples.Benchmarks.QuartzBenchmarks;

public class QuartzExecuteJobsBenchmark : IBenchmark
{
    public string Name => "Quartz.Execute";

    public async Task Run()
    {
        var benchmarkParams = BenchamrkHelper.GetJobsBenchmarkParams(defaultJobsCount: 1000,
            defaultJobName: TestJob.JobName,
            defaultJobDelayMs: 0);

        var dataSource = DataSourceFactory.Create();

        Console.WriteLine("Clear jobs database");
        QuartzHelper.RemoveAllJobs(dataSource);

        Console.WriteLine($"Create {benchmarkParams.JobsCount} {benchmarkParams.JobName}");

        var scheduler = await QuartzHelper.CreateScheduler();

        for (int i = 1; i <= benchmarkParams.JobsCount; i++)
        {
            var jobParam = new TestJobParam
            {
                Id = i,
                Value = Guid.NewGuid().ToString(),
                DelayMs = benchmarkParams.JobDelayMs,
            };
            await QuartzHelper.CreateTestJob(scheduler, jobParam);
        }
        
        
        Console.WriteLine("Start quartz server");

        Stopwatch stopwatch = Stopwatch.StartNew();

        await scheduler.Start();
        
        var hasNotCompletedJobs = true;
        while (hasNotCompletedJobs)
        {
            hasNotCompletedJobs = QuartzHelper.HasNotCompletedJobs(dataSource);
            if (hasNotCompletedJobs)
            {
                await Task.Delay(100);
            }
        }

        stopwatch.Stop();

        await scheduler.Shutdown();

        Console.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds} ms");
    }
}
