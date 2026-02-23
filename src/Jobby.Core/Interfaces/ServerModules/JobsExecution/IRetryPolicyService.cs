using Jobby.Core.Models;

namespace Jobby.Core.Interfaces.ServerModules.JobsExecution;

internal interface IRetryPolicyService
{
    RetryPolicy GetRetryPolicy(JobExecutionModel job);
}
