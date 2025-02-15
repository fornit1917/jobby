namespace Jobby.Samples.Benchmarks.HangfireBenchmarks;

internal class HangfireTestJob
{
    public const string JobName = "TestJob";

    public async Task Execute(HangfireTestJobParam? jobParam)
    {
        if (jobParam?.DelayMs > 0)
        {
            await Task.Delay(jobParam.DelayMs);
        }
    }
}
