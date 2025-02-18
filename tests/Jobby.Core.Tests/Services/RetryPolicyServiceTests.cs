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

        var policyService = new RetryPolicyService(defaultPolicy);

        var job = new JobModel { JobName = "Name" };

        job.StartedCount = 1;
        var interval1 = policyService.GetRetryInterval(job);
        Assert.Equal(TimeSpan.FromSeconds(10), interval1);

        job.StartedCount = 2;
        var interval2 = policyService.GetRetryInterval(job);
        Assert.Equal(TimeSpan.FromSeconds(20), interval2);

        job.StartedCount = 3;
        var interval3 = policyService.GetRetryInterval(job);
        Assert.Equal(TimeSpan.FromSeconds(20), interval3);

        job.StartedCount = 4;
        var interval4 = policyService.GetRetryInterval(job);
        Assert.Null(interval4);
    }

    [Fact]
    public void HasPolicyForJobName_UsesPolicyForJobName()
    {
        var job = new JobModel { JobName = "Name" };


        var defaultPolicy = new RetryPolicy
        {
            IntervalsSeconds = [1, 2],
            MaxCount = 4
        };

        var policiesByJobName = new Dictionary<string, RetryPolicy>
        {
            [job.JobName] = new RetryPolicy
            {
                IntervalsSeconds = [10, 20],
                MaxCount = 4
            },
        };
       
        var policyService = new RetryPolicyService(defaultPolicy, policiesByJobName);

        job.StartedCount = 1;
        var interval1 = policyService.GetRetryInterval(job);
        Assert.Equal(TimeSpan.FromSeconds(10), interval1);

        job.StartedCount = 2;
        var interval2 = policyService.GetRetryInterval(job);
        Assert.Equal(TimeSpan.FromSeconds(20), interval2);

        job.StartedCount = 3;
        var interval3 = policyService.GetRetryInterval(job);
        Assert.Equal(TimeSpan.FromSeconds(20), interval3);

        job.StartedCount = 4;
        var interval4 = policyService.GetRetryInterval(job);
        Assert.Null(interval4);
    }
}
