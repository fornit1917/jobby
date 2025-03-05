using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Builders;
using Jobby.Core.Models;
using System.Collections.Frozen;
using System.Reflection;

namespace Jobby.Core.Services.Builders;

public class JobsRegistryBuilder : IJobsRegistryConfigurable, IJobsRegistryBuilder
{
    private readonly Dictionary<string, CommandExecutionMetadata> _cmdExecMetadataByJobName = new();
    private readonly Dictionary<string, RecurrentJobExecutionMetadata> _recurrentExecMetadataByJobName = new();

    public IJobsRegistryConfigurable AddCommand<TCommand, THandler>()
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

        var execMetadata = new CommandExecutionMetadata
        {
            CommandType = typeof(TCommand),
            HandlerType = handlerType,
            ExecMethod = execMethod
        };

        _cmdExecMetadataByJobName[jobName] = execMetadata;

        return this;
    }

    public IJobsRegistryConfigurable AddRecurrentJob<THandler>() where THandler : IRecurrentJobHandler
    {
        var jobName = THandler.GetRecurrentJobName();
        var handlerType = typeof(THandler);
        // todo: use more strict search criteria for the method ExecuteAsync
        var execMethod = handlerType.GetMethod("ExecuteAsync");
        if (execMethod == null)
        {
            throw new ArgumentException($"Type {handlerType} does not have suitable ExecuteAsync method");
        }

        var execMetadata = new RecurrentJobExecutionMetadata
        {
            HandlerType = handlerType,
            ExecMethod = execMethod
        };

        _recurrentExecMetadataByJobName[jobName] = execMetadata;

        return this;
    }

    public IJobsRegistryConfigurable AddJobsFromAssemblies(params Assembly[] assemblies)
    {
        // todo: implement it
        throw new NotImplementedException();
    }

    public IJobsRegistry Build()
    {
        return new JobsRegistry(_cmdExecMetadataByJobName.ToFrozenDictionary(),
            _recurrentExecMetadataByJobName.ToFrozenDictionary());
    }
}
