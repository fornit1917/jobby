namespace Jobby.Benchmarks;

internal class BenchmarkParams
{
    public int JobsCount { get; set; } = 1000;
    public int DegreeOfParallelism { get; set; } = 10;
    public bool CompleteWithBatching { get; set; }
    public bool DisableSerializableGroups { get; set; } = false;
}
