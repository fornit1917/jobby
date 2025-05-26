using Jobby.Core.Models;
using Jobby.Core.Services;

namespace Jobby.Core.Tests.Services;

public class RetryPolicyServiceTests
{
    [Fact]
    public void DoesNotHavePolicyForJobName_UsesDefaultPolicy()
    {
        var defaultPolicy = new RetryPolicy
        {
            IntervalsSeconds = [10, 20],
            MaxCount = 4
        };
        var policyService = new RetryPolicyService(defaultPolicy, new Dictionary<string, RetryPolicy>());

        var job = new JobExecutionModel { JobName = "Name" };
        var policy = policyService.GetRetryPolicy(job);

        Assert.Equal(defaultPolicy, policy);
    }

    [Fact]
    public void HasPolicyForJobName_UsesPolicyForJobName()
    {
        var job = new JobExecutionModel { JobName = "Name" };

        var defaultPolicy = new RetryPolicy
        {
            IntervalsSeconds = [1, 2],
            MaxCount = 4
        };

        var jobSpecificPolicy = new RetryPolicy
        {
            IntervalsSeconds = [10, 20],
            MaxCount = 4
        };
        var policiesByJobName = new Dictionary<string, RetryPolicy>
        {
            [job.JobName] = jobSpecificPolicy 
        };
        var policyService = new RetryPolicyService(defaultPolicy, policiesByJobName);

        var policy = policyService.GetRetryPolicy(job);

        Assert.Equal(jobSpecificPolicy, policy);
    }
}
