namespace Jobby.Core.Interfaces;

internal interface IJobsRegistry
{
    IJobExecutor? GetJobExecutor(string jobName);
}
