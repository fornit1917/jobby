using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Core.Services;

internal class JobsRegistry : IJobsRegistry
{
    private readonly IReadOnlyDictionary<string, JobExecutionMetadata> _cmdExecMetadataByJobName;

    public JobsRegistry(IReadOnlyDictionary<string, JobExecutionMetadata> execMetadataByJobName)
    {
        _cmdExecMetadataByJobName = execMetadataByJobName;
    }

    public JobExecutionMetadata? GetJobExecutionMetadata(string jobName)
    {
        _cmdExecMetadataByJobName.TryGetValue(jobName, out JobExecutionMetadata? jobExecutionMetadata);
        return jobExecutionMetadata;
    }
}
