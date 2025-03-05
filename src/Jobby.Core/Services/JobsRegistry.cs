using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using System.Collections.Frozen;

namespace Jobby.Core.Services;

internal class JobsRegistry : IJobsRegistry
{
    private readonly IReadOnlyDictionary<string, CommandExecutionMetadata> _cmdExecMetadataByJobName;
    private readonly IReadOnlyDictionary<string, RecurrentJobExecutionMetadata> _recurrentExecMetadataByJobName;

    public JobsRegistry(IReadOnlyDictionary<string, CommandExecutionMetadata> execMetadataByJobName,
        IReadOnlyDictionary<string, RecurrentJobExecutionMetadata> recurrentExecMetadataByJobName)
    {
        _cmdExecMetadataByJobName = execMetadataByJobName;
        _recurrentExecMetadataByJobName = recurrentExecMetadataByJobName;
    }

    public CommandExecutionMetadata? GetCommandExecutionMetadata(string jobName)
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
