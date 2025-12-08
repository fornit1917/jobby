
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.HandlerPipeline;
using Jobby.Core.Models;
using Jobby.Samples.AspNet.Exceptions;

namespace Jobby.Samples.AspNet.JobsMiddlewares;

public class IgnoreSomeErrorsMiddleware : IJobbyMiddleware
{
    public async Task ExecuteAsync<TCommand>(TCommand command, JobExecutionContext ctx, IJobCommandHandler<TCommand> handler) 
        where TCommand : IJobCommand
    {
        try
        {
            await handler.ExecuteAsync(command, ctx);
        }
        catch(ExceptionShouldBeIgnored ex)
        {
        }
    }
}
