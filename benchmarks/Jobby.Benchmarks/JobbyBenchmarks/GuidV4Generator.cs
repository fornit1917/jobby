using Jobby.Core.Interfaces;

namespace Jobby.Benchmarks.JobbyBenchmarks;

public class GuidV4Generator : IGuidGenerator
{
    public Guid NewGuid()
    {
        return Guid.NewGuid();
    }
}