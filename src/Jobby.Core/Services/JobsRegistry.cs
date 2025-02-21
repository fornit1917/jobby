using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using System.Collections.Frozen;

namespace Jobby.Core.Services;

internal class JobsRegistry : IJobsRegistry
{
    private readonly FrozenDictionary<string, JobExecutionMetadata> _execMetadataByJobName;

    public JobsRegistry(FrozenDictionary<string, JobExecutionMetadata> execMetadataByJobName)
    {
        _execMetadataByJobName = execMetadataByJobName;
    }

    public JobExecutionMetadata? GetJobExecutionMetadata(string jobName)
    {
        _execMetadataByJobName.TryGetValue(jobName, out JobExecutionMetadata? jobExecutionMetadata);
        return jobExecutionMetadata;
    }
}
