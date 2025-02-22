﻿using Jobby.Core.Models;

namespace Jobby.Core.Tests.Models;

public class RetryPolicyTests
{
    [Fact]
    public void IsLastAttempt_ReturnsCorrectValue()
    {
        var retryPolicy = new RetryPolicy
        {
            MaxCount = 4,
            IntervalsSeconds = [10, 20]
        };

        var job = new Job();

        job.StartedCount = 1;
        Assert.False(retryPolicy.IsLastAttempt(job));

        job.StartedCount = 2;
        Assert.False(retryPolicy.IsLastAttempt(job));

        job.StartedCount = 3;
        Assert.False(retryPolicy.IsLastAttempt(job));

        job.StartedCount = 4;
        Assert.True(retryPolicy.IsLastAttempt(job));
    }

    [Fact]
    public void GetIntervalForNextAttempt()
    {
        var retryPolicy = new RetryPolicy
        {
            MaxCount = 4,
            IntervalsSeconds = [10, 20]
        };

        var job = new Job();

        job.StartedCount = 1;
        var interval1 = retryPolicy.GetIntervalForNextAttempt(job);
        Assert.Equal(TimeSpan.FromSeconds(10), interval1);

        job.StartedCount = 2;
        var interval2 = retryPolicy.GetIntervalForNextAttempt(job);
        Assert.Equal(TimeSpan.FromSeconds(20), interval2);

        job.StartedCount = 3;
        var interval3 = retryPolicy.GetIntervalForNextAttempt(job);
        Assert.Equal(TimeSpan.FromSeconds(20), interval3);

        job.StartedCount = 4;
        var interval4 = retryPolicy.GetIntervalForNextAttempt(job);
        Assert.Null(interval4);
    }
}
