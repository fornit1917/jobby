using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using System.Collections.Frozen;

namespace Jobby.Core.Services;

public class JobsRegistryBuilder : IJobsRegistryBuilder
{
    private readonly Dictionary<string, JobExecutionMetadata> _execMetadataByJobName = new Dictionary<string, JobExecutionMetadata>();

    public IJobsRegistryBuilder AddJob<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>
    {
        var jobName = TCommand.GetJobName();
        var handlerType = typeof(IJobCommandHandler<TCommand>);
        // todo: use more strict search criteria for the method ExecuteAsync
        var execMethod = handlerType.GetMethod("ExecuteAsync");
        if (execMethod == null)
        {
            throw new ArgumentException($"Type {handlerType} does not have suitable ExecuteAsync method");
        }

        var execMetadata = new JobExecutionMetadata
        {
            CommandType = typeof(TCommand),
            HandlerType = handlerType,
            ExecMethod = execMethod
        };

        _execMetadataByJobName[jobName] = execMetadata;

        return this;
    }

    public IJobsRegistry Build()
    {
        return new JobsRegistry(_execMetadataByJobName.ToFrozenDictionary());
    }
}
