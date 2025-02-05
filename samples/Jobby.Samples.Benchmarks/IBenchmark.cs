namespace Jobby.Samples.Benchmarks;

public interface IBenchmark
{
    string Name { get; }
    Task Run();
}
