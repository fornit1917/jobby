using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Builders;
using Jobby.Core.Models;
using System.Collections.Frozen;
using System.Reflection;

namespace Jobby.Core.Services.Builders;

internal class JobsRegistryBuilder : IJobsRegistryConfigurable, IJobsRegistryBuilder
{
    private readonly Dictionary<string, CommandExecutionMetadata> _cmdExecMetadataByJobName = new();
    private readonly Dictionary<string, RecurrentJobExecutionMetadata> _recurrentExecMetadataByJobName = new();

    public IJobsRegistryConfigurable AddCommand<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>
    {
        var jobName = TCommand.GetJobName();
        var handlerType = typeof(IJobCommandHandler<TCommand>);
        var execMethod = handlerType.GetMethod("ExecuteAsync", [typeof(TCommand), typeof(CommandExecutionContext)]);
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
        var execMethod = handlerType.GetMethod("ExecuteAsync", [typeof(RecurrentJobExecutionContext)]);
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
