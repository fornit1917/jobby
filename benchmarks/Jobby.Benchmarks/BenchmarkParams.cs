namespace Jobby.Benchmarks;

internal class BenchmarkParams
{
    public int DegreeOfParallelism { get; set; } = 10;
    public bool CompleteWithBatching { get; set; }

}
