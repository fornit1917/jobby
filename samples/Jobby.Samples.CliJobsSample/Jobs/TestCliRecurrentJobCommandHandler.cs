using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Samples.CliJobsSample.Jobs;

internal class TestCliRecurrentJobCommandHandler : IJobCommandHandler<TestCliRecurrentJobCommand>
{
    public Task ExecuteAsync(TestCliRecurrentJobCommand command, JobExecutionContext ctx)
    {
        if (command.Value == null)
        {
            Console.WriteLine($"Recurrent Job {ctx.JobName} executed, {DateTime.Now}");
        }
        else
        {
            Console.WriteLine($"Recurrent Job {ctx.JobName} executed, value={command.Value}, {DateTime.Now}");
        }
                
        
        return Task.CompletedTask;
    }
}
