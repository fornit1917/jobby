using Jobby.Core.Interfaces;
using Jobby.Samples.CliJobsSample.Jobs;

namespace Jobby.Samples.CliJobsSample.HandlerFactory;

internal class SimpleJobExecutionScope : IJobExecutionScope
{
    public void Dispose()
    {
    }

    public object? GetService(Type type)
    {
        if (type == typeof(IJobCommandHandler<TestCliJobCommand>))
            return new TestCliJobCommandHandler();

        if (type == typeof(IJobCommandHandler<TestCliRecurrentJobCommand>))
            return new TestCliRecurrentJobCommandHandler();

        return null;
    }
}
