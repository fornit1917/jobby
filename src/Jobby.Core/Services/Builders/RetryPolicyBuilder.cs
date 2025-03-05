using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Builders;
using Jobby.Core.Models;
using System.Collections.Frozen;

namespace Jobby.Core.Services.Builders;

public class RetryPolicyBuilder : IRetryPolicyConfigurable, IRetryPolicyBuilder
{
    private RetryPolicy _defaultRetryPolicy = RetryPolicy.NoRetry;
    private readonly Dictionary<string, RetryPolicy> _retryPoliciesByJobName = new();

    public IRetryPolicyService Build()
    {
        return new RetryPolicyService(_defaultRetryPolicy, _retryPoliciesByJobName.ToFrozenDictionary());
    }

    public IRetryPolicyConfigurable UseByDefault(RetryPolicy retryPolicy)
    {
        _defaultRetryPolicy = retryPolicy;
        return this;
    }

    public IRetryPolicyConfigurable UseForJob(string jobName, RetryPolicy retryPolicy)
    {
        _retryPoliciesByJobName[jobName] = retryPolicy;
        return this;
    }
}
