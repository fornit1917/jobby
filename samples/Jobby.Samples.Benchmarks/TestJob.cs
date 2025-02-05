namespace Jobby.Samples.Benchmarks;

internal class TestJob
{
    public const string JobName = "TestJob";

    public async Task Execute(TestJobParam? jobParam)
    {
        if (jobParam?.DelayMs > 0)
        {
            await Task.Delay(jobParam.DelayMs);
        }
    }
}
