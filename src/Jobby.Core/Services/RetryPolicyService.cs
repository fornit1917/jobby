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

    public TimeSpan? GetRetryInterval(JobModel job)
    {
        RetryPolicy? retryPolicy = null;

        // todo: try get policy from job instance
        
        _retryPoliciesByJobName.TryGetValue(job.JobName, out retryPolicy);
        if (retryPolicy == null)
        {
            retryPolicy = _defaultRetryPolicy;
        }

        if (job.StartedCount >= retryPolicy.MaxCount)
        {
            return null;
        }

        var intervalSeconds = 1;
        if (retryPolicy.IntervalsSeconds.Count > 0) 
        {
            intervalSeconds = retryPolicy.IntervalsSeconds.Count >= job.StartedCount
                ? retryPolicy.IntervalsSeconds[job.StartedCount - 1] 
                : retryPolicy.IntervalsSeconds[retryPolicy.IntervalsSeconds.Count - 1];
        }

        return TimeSpan.FromSeconds(intervalSeconds);
    }
}
