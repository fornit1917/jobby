using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.TestsUtils.Jobs;

public class TestJobCommandHandler : IJobCommandHandler<TestJobCommand>
{
    public TestJobCommand? LatestCommand { get; private set; } = null;

    public async Task ExecuteAsync(TestJobCommand command, JobExecutionContext ctx)
    {
        LatestCommand = command;
        await Task.Delay(0);
        if (command.ExceptionToThrow != null)
        {
            throw command.ExceptionToThrow;
        }
    }
}
