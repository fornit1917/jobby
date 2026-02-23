using Jobby.Core.Models;

namespace Jobby.Core.Interfaces.ServerModules.JobsExecution;

internal interface IJobCompletionService
{
    Task CompleteJob(JobExecutionModel job);
}
