using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using System.Collections.Frozen;

namespace Jobby.Core.Services;

internal class JobsRegistry : IJobsRegistry
{
    private readonly FrozenDictionary<string, CommandExecutionMetadata> _cmdExecMetadataByJobName;
    private readonly FrozenDictionary<string, RecurrentJobExecutionMetadata> _recurrentExecMetadataByJobName;

    public JobsRegistry(FrozenDictionary<string, CommandExecutionMetadata> execMetadataByJobName, 
        FrozenDictionary<string, RecurrentJobExecutionMetadata> recurrentExecMetadataByJobName)
    {
        _cmdExecMetadataByJobName = execMetadataByJobName;
        _recurrentExecMetadataByJobName = recurrentExecMetadataByJobName;
    }

    public CommandExecutionMetadata? GetJobExecutionMetadata(string jobName)
    {
        _cmdExecMetadataByJobName.TryGetValue(jobName, out CommandExecutionMetadata? jobExecutionMetadata);
        return jobExecutionMetadata;
    }

    public RecurrentJobExecutionMetadata? GetRecurrentJobExecutionMetadata(string jobName)
    {
        _recurrentExecMetadataByJobName.TryGetValue(jobName, out var recurrentJobExecutionMetadata);
        return recurrentJobExecutionMetadata;
    }
}
