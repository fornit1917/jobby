using Jobby.Core.Models;

namespace Jobby.Core.Interfaces.Builders;

public interface IRetryPolicyConfigurable
{
    IRetryPolicyConfigurable UseByDefault(RetryPolicy retryPolicy);
    IRetryPolicyConfigurable UseForJob(string jobName, RetryPolicy retryPolicy);
}
