using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.TestsUtils.Jobs;

namespace Jobby.Tests.Core.Services;
public class JobExecutorTests
{
    [Fact]
    public async Task ExecuteJob_InvokeHandlerWithSpecifiedCommand()
    {
        var command = new TestJobCommand();
        var handler = new TestJobCommandHandler();

        IJobExecutor jobExecutor = new JobExecutor<TestJobCommand>(
            command,
            handler
        );

        var ctx = new JobExecutionContext
        {
            JobName = "TestJobName",
            StartedCount = 0,
            IsLastAttempt = false,
            CancellationToken = CancellationToken.None,
        };


        await jobExecutor.ExecuteJob(ctx);

        Assert.Equal(command, handler.LatestCommand);
    }
}
