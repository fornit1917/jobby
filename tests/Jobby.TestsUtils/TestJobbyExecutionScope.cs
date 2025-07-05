using Jobby.Core.Interfaces;
using Jobby.TestsUtils.Jobs;

namespace Jobby.TestsUtils;

public class TestJobbyExecutionScope : IJobExecutionScope
{
    private readonly ExecutedCommandsList _executedCommands;

    public TestJobbyExecutionScope(ExecutedCommandsList executedCommands)
    {
        _executedCommands = executedCommands;
    }

    public void Dispose()
    {
    }

    public object? GetService(Type type)
    {
        if (type == typeof(IJobCommandHandler<TestJobCommand>))
            return new TestJobCommandHandler(_executedCommands);

        return null;
    }
}
