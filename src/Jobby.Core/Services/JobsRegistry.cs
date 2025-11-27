using Jobby.Core.Interfaces;

namespace Jobby.Core.Services;

internal class JobsRegistry : IJobsRegistry
{
    private readonly IReadOnlyDictionary<string, IJobExecutor> _jobExecutorsByJobName;

    public JobsRegistry(IReadOnlyDictionary<string, IJobExecutor> jobExecutorsByJobName)
    {
        _jobExecutorsByJobName = jobExecutorsByJobName;
    }

    public IJobExecutor? GetJobExecutor(string jobName)
        => _jobExecutorsByJobName.GetValueOrDefault(jobName);
}
