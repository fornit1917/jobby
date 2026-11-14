using Jobby.Core.Interfaces.ServerModules.JobsExecution;
using Jobby.Core.Models;

namespace Jobby.Core.Services.ServerModules.JobsExecution;

internal class RetryPolicyService : IRetryPolicyService
{
    private readonly RetryPolicy _defaultRetryPolicy;
    private readonly IReadOnlyDictionary<string, RetryPolicy> _retryPoliciesByJobName;

    public RetryPolicyService(RetryPolicy defaultPolicy, IReadOnlyDictionary<string, RetryPolicy> retryPoliciesByJobName)
    {
        _defaultRetryPolicy = defaultPolicy;
        _retryPoliciesByJobName = retryPoliciesByJobName;
    }


    public RetryPolicy GetRetryPolicy(JobExecutionModel job)
    {
        _retryPoliciesByJobName.TryGetValue(job.JobName, out var retryPolicy);
        retryPolicy ??= _defaultRetryPolicy;
        return retryPolicy;
    }
}
