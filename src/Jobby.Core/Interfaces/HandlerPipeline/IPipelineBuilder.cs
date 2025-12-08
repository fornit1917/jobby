using Jobby.Core.Services.HandlerPipeline;

namespace Jobby.Core.Interfaces.HandlerPipeline;

internal interface IPipelineBuilder
{
    IJobCommandHandler<TCommand> Build<TCommand>(IJobCommandHandler<TCommand> innerHandler, IJobExecutionScope scope) 
        where TCommand: IJobCommand;
}
