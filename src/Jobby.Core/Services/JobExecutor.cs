using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Core.Services;

internal class JobExecutor<TCommand, THandler> : IJobExecutor
    where TCommand : IJobCommand
    where THandler : IJobCommandHandler<TCommand>
{
    Task IJobExecutor.Execute(JobExecutionModel job,
        JobExecutionContext ctx,
        IJobExecutionScope scope,
        IJobParamSerializer serializer)
    {
        var handlerInstance = (THandler?)scope.GetService(typeof(IJobCommandHandler<TCommand>));
        if (handlerInstance == null)
        {
            throw new InvalidJobHandlerException($"Could not create instance of handler with type {typeof(THandler)}");
        }

        var command = (TCommand?)serializer.DeserializeJobParam(job.JobParam, typeof(TCommand));
        if (command == null)
        {
            throw new InvalidJobHandlerException($"Could not deserialize job parameter with type {typeof(TCommand)}");
        }

        return handlerInstance.ExecuteAsync(command, ctx);
    }

    public JobTypesMetadata GetJobTypesMetadata() => new JobTypesMetadata(typeof(TCommand), 
        typeof(IJobCommandHandler<TCommand>), 
        typeof(THandler));

}
