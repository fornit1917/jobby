using Jobby.Core.Models;

namespace Jobby.Core.Interfaces.ServerModules.JobsExecution;

internal interface IJobExecutionService
{
    Task ExecuteJob(JobExecutionModel job, CancellationToken cancellationToken);
}
