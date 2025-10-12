namespace Jobby.Core.Interfaces;

internal interface IJobsRegistry
{
    IJobExecutorFactory? GetJobExecutorFactory(string jobName);
}
