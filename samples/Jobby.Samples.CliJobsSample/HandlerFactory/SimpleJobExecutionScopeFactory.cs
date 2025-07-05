using Jobby.Core.Interfaces;

namespace Jobby.Samples.CliJobsSample.HandlerFactory;

internal class SimpleJobExecutionScopeFactory : IJobExecutionScopeFactory
{
    public IJobExecutionScope CreateJobExecutionScope()
    {
        return new SimpleJobExecutionScope();
    }
}
