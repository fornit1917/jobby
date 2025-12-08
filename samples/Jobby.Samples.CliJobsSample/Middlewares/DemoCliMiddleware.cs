using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.HandlerPipeline;
using Jobby.Core.Models;
using Jobby.Samples.CliJobsSample.Jobs;

namespace Jobby.Samples.CliJobsSample.Middlewares;

internal class DemoCliMiddleware : IJobbyMiddleware
{
    public async Task ExecuteAsync<TCommand>(TCommand command, JobExecutionContext ctx, IJobCommandHandler<TCommand> handler)
        where TCommand : IJobCommand
    {
        if (command is TestCliJobCommand testCliJobCommand)
        {
            Console.WriteLine($"Middleware, TestCliJobCommand, Id = {testCliJobCommand.Id}");
        }

        await handler.ExecuteAsync(command, ctx);
    }
}
