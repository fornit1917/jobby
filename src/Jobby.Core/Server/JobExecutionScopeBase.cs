using Jobby.Abstractions.CommonServices;
using Jobby.Abstractions.Models;
using Jobby.Abstractions.Server;
using Jobby.Core.Exceptions;
using System.Reflection;

namespace Jobby.Core.Server;

public abstract class JobExecutionScopeBase : IJobExecutionScope
{
    private readonly IJobParamSerializer _serializer;

    private readonly IReadOnlyDictionary<string, Type> _jobCommandTypesByName;
    private readonly IReadOnlyDictionary<Type, Type> _handlerTypesByCommandType;

    protected JobExecutionScopeBase(IReadOnlyDictionary<string, Type> jobCommandTypesByName,
        IReadOnlyDictionary<Type, Type> handlerTypesByCommandType,
        IJobParamSerializer serializer)
    {
        _jobCommandTypesByName = jobCommandTypesByName;
        _handlerTypesByCommandType = handlerTypesByCommandType;
        _serializer = serializer;
    }

    public Task ExecuteAsync(JobModel jobModel)
    {

        if (_jobCommandTypesByName.TryGetValue(jobModel.JobName, out var commandType))
        {
            MethodInfo? executeMethod;

            if (_handlerTypesByCommandType.TryGetValue(commandType, out var handlerType))
            {
                var handler = CreateService(handlerType);
                if (handler == null)
                {
                    throw new InvalidJobHandlerException($"Could not create an instance of {handlerType}");
                }

                // todo: use more strict search criteria for the method ExecuteAsync
                executeMethod = handlerType.GetMethod("ExecuteAsync");
                if (executeMethod == null)
                {
                    throw new InvalidJobHandlerException($"Handler ${handler.GetType()} does not have method ExecuteAsync");
                }

                var command = _serializer.DeserializeJobParam(jobModel.JobParam, commandType);

                var result = executeMethod.Invoke(handler, [command]);
                if (result is Task)
                {
                    return (Task)result;
                }
            }
            else
            {
                throw new InvalidJobHandlerException($"Could not find handler type for command {commandType}");
            }
        }

        throw new InvalidJobHandlerException($"Could not find handler for job {jobModel.JobName}");
    }

    protected abstract object? CreateService(Type t);

    public abstract void Dispose();
}
