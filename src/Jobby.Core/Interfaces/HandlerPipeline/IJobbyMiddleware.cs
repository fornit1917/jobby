using Jobby.Core.Models;

namespace Jobby.Core.Interfaces.HandlerPipeline;

public interface IJobbyMiddleware
{
    Task ExecuteAsync<TCommand>(TCommand command, JobExecutionContext ctx, IJobCommandHandler<TCommand> handler) where TCommand : IJobCommand;
}
