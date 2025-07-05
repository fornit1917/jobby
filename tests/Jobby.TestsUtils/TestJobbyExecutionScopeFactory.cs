using Jobby.Core.Interfaces;

namespace Jobby.TestsUtils;

public class TestJobbyExecutionScopeFactory : IJobExecutionScopeFactory
{
    private readonly ExecutedCommandsList _executedCommands;

    public TestJobbyExecutionScopeFactory(ExecutedCommandsList executedCommands)
    {
        _executedCommands = executedCommands;
    }

    public IJobExecutionScope CreateJobExecutionScope()
    {
        return new TestJobbyExecutionScope(_executedCommands);
    }
}
