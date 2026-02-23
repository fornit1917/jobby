namespace Jobby.Core.Interfaces.ServerModules.JobsExecution;

internal interface IJobsExecutionServerModule
{
    void Start();
    void SendStopSignal();
    bool HasInProgressJobs();
}