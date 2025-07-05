using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.TestsUtils.Jobs;

public class TestNoAsyncJobCommandHandler : IJobCommandHandler<TestNoAsyncJobCommand>
{
    public TestNoAsyncJobCommand? LatestCommand { get; private set; }

    public Task ExecuteAsync(TestNoAsyncJobCommand command, JobExecutionContext ctx)
    {
        LatestCommand = command;

        if (command.ExceptionToThrow != null)
            throw command.ExceptionToThrow;
        return Task.CompletedTask;
    }
}
