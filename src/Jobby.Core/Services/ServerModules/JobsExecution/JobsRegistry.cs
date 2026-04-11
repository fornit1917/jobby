using Jobby.Core.Interfaces.ServerModules.JobsExecution;

namespace Jobby.Core.Services.ServerModules.JobsExecution;

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
