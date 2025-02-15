namespace Jobby.Samples.Benchmarks.HangfireBenchmarks;

public class HangfireTestJobParam
{
    public int Id { get; set; }
    public string Value { get; set; } = string.Empty;
    public int DelayMs { get; set; }
}
