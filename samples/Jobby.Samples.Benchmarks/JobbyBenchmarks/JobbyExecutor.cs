using Jobby.Core.Interfaces;

namespace Jobby.Samples.Benchmarks.JobbyBenchmarks;

public class JobbyTestExecutionScope : IJobExecutionScope
{
    public void Dispose()
    {
    }

    public object? GetService(Type type)
    {
        if (type == typeof(IJobCommandHandler<JobbyTestJobCommand>))
        {
            return new JobbyTestJobCommandHandler();
        }
        return null;
    }
}

public class JobbyTestExecutionScopeFactory : IJobExecutionScopeFactory
{
    public IJobExecutionScope CreateJobExecutionScope()
    {
        return new JobbyTestExecutionScope();
    }
}