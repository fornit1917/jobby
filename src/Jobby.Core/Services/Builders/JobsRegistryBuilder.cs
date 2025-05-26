using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Builders;
using Jobby.Core.Models;
using System.Collections.Frozen;
using System.Reflection;

namespace Jobby.Core.Services.Builders;

internal class JobsRegistryBuilder : IJobsRegistryConfigurable, IJobsRegistryBuilder
{
    private readonly Dictionary<string, JobExecutionMetadata> _cmdExecMetadataByJobName = new();

    public IJobsRegistryConfigurable AddJob<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>
    {
        var jobName = TCommand.GetJobName();
        var handlerType = typeof(IJobCommandHandler<TCommand>);
        var execMethod = handlerType.GetMethod("ExecuteAsync", [typeof(TCommand), typeof(JobExecutionContext)]);
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

        _cmdExecMetadataByJobName[jobName] = execMetadata;

        return this;
    }

    public IJobsRegistryConfigurable AddJobsFromAssemblies(params Assembly[] assemblies)
    {
        // todo: implement it
        throw new NotImplementedException();
    }

    public IJobsRegistry Build()
    {
        return new JobsRegistry(_cmdExecMetadataByJobName.ToFrozenDictionary());
    }
}
