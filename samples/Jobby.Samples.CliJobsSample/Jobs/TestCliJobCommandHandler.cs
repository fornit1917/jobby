using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Samples.CliJobsSample.Jobs;

internal class TestCliJobCommandHandler : IJobCommandHandler<TestCliJobCommand>
{
    public Task ExecuteAsync(TestCliJobCommand command, JobExecutionContext ctx)
    {
        if (command.ShouldBeFailed)
        {
            Console.WriteLine($"Exception will be thrown, Id = {command.Id}");
            throw new Exception("Error message");
        }

        Console.WriteLine($"Executed, Id = {command.Id}");
        return Task.CompletedTask;
    }
}
