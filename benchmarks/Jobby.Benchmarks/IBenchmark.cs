namespace Jobby.Benchmarks;

public interface IBenchmark
{
    string Name { get; }
    Task Run();
}
