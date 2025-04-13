using Quartz;

namespace Jobby.Benchmarks.QuartzBenchmarks;

internal class QuartzTestJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var delayMs = context.MergedJobDataMap.GetInt("DelayMs");
        if (delayMs > 0)
        {
            await Task.Delay(delayMs);
        }
        Counter.Increment();
    }
}
