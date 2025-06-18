using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.TestsUtils.Jobs;

public class TestJobCommandHandler : IJobCommandHandler<TestJobCommand>
{
    public Task ExecuteAsync(TestJobCommand command, JobExecutionContext ctx)
    {
        return Task.CompletedTask;
    }
}
