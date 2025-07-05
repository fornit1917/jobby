using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.TestsUtils.Jobs;

public class TestJobCommandHandler : IJobCommandHandler<TestJobCommand>
{
    private ExecutedCommandsList? _executedCommands;

    public TestJobCommand? LatestCommand { get; private set; } = null;

    public TestJobCommandHandler()
    {
    }

    public TestJobCommandHandler(ExecutedCommandsList executedCommandList)
    {
        _executedCommands = executedCommandList;
    }

    public async Task ExecuteAsync(TestJobCommand command, JobExecutionContext ctx)
    {
        LatestCommand = command;
        _executedCommands?.Add(command);
        await Task.Delay(0);
        if (command.ExceptionToThrow != null)
        {
            throw command.ExceptionToThrow;
        }
    }
}
