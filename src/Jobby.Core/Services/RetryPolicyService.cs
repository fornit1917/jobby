using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using System.Collections.Frozen;

namespace Jobby.Core.Services;

public class RetryPolicyService : IRetryPolicyService
{
    private readonly RetryPolicy _defaultRetryPolicy;
    private readonly IReadOnlyDictionary<string, RetryPolicy> _retryPoliciesByJobName;

    public RetryPolicyService(RetryPolicy defaultPolicy, IReadOnlyDictionary<string, RetryPolicy> retryPoliciesByJobName)
    {
        _defaultRetryPolicy = defaultPolicy ?? new RetryPolicy()
        {
            MaxCount = 10,
            IntervalsSeconds = [60, 120, 240, 480, 600]
        };

        _retryPoliciesByJobName = retryPoliciesByJobName;
    }


    public RetryPolicy GetRetryPolicy(Job job)
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
