using Quartz;

namespace Jobby.Samples.Benchmarks.QuartzBenchmarks;

internal class QuartzTestJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var delayMs = context.MergedJobDataMap.GetInt("DelayMs");
        if (delayMs > 0)
        {
            await Task.Delay(delayMs);
        }
    }
}
