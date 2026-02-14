using Jobby.Core.Interfaces;

namespace Jobby.Samples.TestConcurrency;

public class ConcurrentJobExecutionScopeFactory : IJobExecutionScopeFactory
{
    public IJobExecutionScope CreateJobExecutionScope()
    {
        return new ConcurrentJobExecutionScope();
    }
}

public class ConcurrentJobExecutionScope : IJobExecutionScope
{
    public object? GetService(Type type)
    {
        if (type == typeof(IJobCommandHandler<ConcurrentJobCommand>))
        {
            return new ConcurrentJobCommandHandler();
        }
        return null;
    }
    
    public void Dispose()
    {
    }
}