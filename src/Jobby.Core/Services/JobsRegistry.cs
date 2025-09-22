using Jobby.Core.Interfaces;

namespace Jobby.Core.Services;

internal class JobsRegistry : IJobsRegistry
{
    private readonly IReadOnlyDictionary<string, IJobExecutorFactory> _cmdExecMetadataByJobName;

    public JobsRegistry(IReadOnlyDictionary<string, IJobExecutorFactory> execMetadataByJobName)
    {
        _cmdExecMetadataByJobName = execMetadataByJobName;
    }

    public IJobExecutorFactory? GetJobExecutorFactory(string jobName)
        => _cmdExecMetadataByJobName.GetValueOrDefault(jobName);
}
