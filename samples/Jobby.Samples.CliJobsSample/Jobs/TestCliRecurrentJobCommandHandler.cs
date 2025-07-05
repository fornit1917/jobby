using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Samples.CliJobsSample.Jobs;

internal class TestCliRecurrentJobCommandHandler : IJobCommandHandler<TestCliRecurrentJobCommand>
{
    public Task ExecuteAsync(TestCliRecurrentJobCommand command, JobExecutionContext ctx)
    {
        Console.WriteLine($"Recurrent Job {ctx.JobName} executed, {DateTime.Now}");
        return Task.CompletedTask;
    }
}
