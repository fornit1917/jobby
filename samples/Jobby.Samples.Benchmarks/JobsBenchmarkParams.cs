namespace Jobby.Samples.Benchmarks;

public class JobsBenchmarkParams
{
    public int JobsCount { get; init; }
    public string JobName { get; init; } = TestJob.JobName;
    public int JobDelayMs { get; init; }
}
