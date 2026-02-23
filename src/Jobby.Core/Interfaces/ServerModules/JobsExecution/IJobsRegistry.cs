namespace Jobby.Core.Interfaces.ServerModules.JobsExecution;

internal interface IJobsRegistry
{
    IJobExecutor? GetJobExecutor(string jobName);
}
