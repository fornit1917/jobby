using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Core.Services;

public class RetryPolicyService : IRetryPolicyService
{
    private readonly RetryPolicy _defaultRetryPolicy;
    private readonly IReadOnlyDictionary<string, RetryPolicy> _retryPoliciesByJobName;

    public RetryPolicyService(RetryPolicy? defaultPolicy = null,
        IReadOnlyDictionary<string, RetryPolicy>? retryPoliciesByJobName = null)
    {
        _defaultRetryPolicy = defaultPolicy ?? new RetryPolicy()
        {
            MaxCount = 10,
            IntervalsSeconds = [60, 120, 240, 480, 600]
        };

        _retryPoliciesByJobName = retryPoliciesByJobName ?? new Dictionary<string, RetryPolicy>();
    }


    public RetryPolicy GetRetryPolicy(JobModel job)
    {
        RetryPolicy? retryPolicy = null;

        // todo: try get policy from job instance

        _retryPoliciesByJobName.TryGetValue(job.JobName, out retryPolicy);
        if (retryPolicy == null)
        {
            retryPolicy = _defaultRetryPolicy;
        }

        return retryPolicy;
    }
}
