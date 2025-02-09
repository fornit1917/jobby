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
        _scheduler = SchedulerBuilder.Create()
            .UseDefaultThreadPool(x => x.MaxConcurrency = 10)
            .UsePersistentStore(x =>
            {
                x.UseClustering();
                x.UseSystemTextJsonSerializer();
                x.UsePostgres(p =>
                {
                    p.ConnectionString = DataSourceFactory.ConnectionString;
                    p.TablePrefix = "qrtz_";
                });
               
            })
            .BuildScheduler()
            .GetAwaiter()
            .GetResult();
    }

    [Benchmark]
    public void QuartzCreateJobs()
    {
        const int jobsCount = 5;
        for (int i = 1; i <= jobsCount; i++) 
        {
            var trigger = TriggerBuilder.Create()
                .WithIdentity(Guid.NewGuid().ToString())
                .StartNow()
                .Build();

            var job = JobBuilder.Create<QuartzTestJob>()
                .WithIdentity(Guid.NewGuid().ToString())
                .UsingJobData(nameof(TestJobParam.Id), i)
                .UsingJobData(nameof(TestJobParam.Value), Guid.NewGuid().ToString())
                .UsingJobData(nameof(TestJobParam.DelayMs), 0)
                .Build();

            _scheduler.ScheduleJob(job, trigger).Wait();
        }
    }
}