using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Core.Services;
internal class JobExecutorFactory<TCommand, THandler> : IJobExecutorFactory
    where TCommand : IJobCommand
    where THandler : IJobCommandHandler<TCommand>
{
    IJobExecutor IJobExecutorFactory.CreateJobExecutor(IJobExecutionScope scope, IJobParamSerializer serializer, string? jobParam)
    {
        var handlerInstance = (THandler?)scope.GetService(typeof(IJobCommandHandler<TCommand>));
        if (handlerInstance == null)
        {
            throw new InvalidJobHandlerException($"Could not create instance of handler with type {typeof(THandler)}");
        }

        var command = (TCommand?)serializer.DeserializeJobParam(jobParam, typeof(TCommand));
        if (command == null)
        {
            throw new InvalidJobHandlerException($"Could not deserialize job parameter with type {typeof(TCommand)}");
        }

        return new JobExecutor<TCommand>(command, handlerInstance);
    }

    public JobTypesMetadata GetJobTypesMetadata() => new JobTypesMetadata(typeof(TCommand), 
        typeof(IJobCommandHandler<TCommand>), 
        typeof(THandler));
}
