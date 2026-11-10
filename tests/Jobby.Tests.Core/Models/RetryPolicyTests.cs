using Jobby.Core.Models;

namespace Jobby.Tests.Core.Models;

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

        var job = new JobExecutionModel { StartedCount = 1 };
        Assert.False(retryPolicy.IsLastAttempt(job));

        job = new JobExecutionModel { StartedCount = 2 };
        Assert.False(retryPolicy.IsLastAttempt(job));

        job = new JobExecutionModel { StartedCount = 3 };
        Assert.False(retryPolicy.IsLastAttempt(job));

        job = new JobExecutionModel { StartedCount = 4 };
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

        var job = new JobExecutionModel { StartedCount = 1 };
        var interval1 = retryPolicy.GetIntervalForNextAttempt(job);
        Assert.Equal(TimeSpan.FromSeconds(10), interval1);

        job = new JobExecutionModel { StartedCount = 2 };
        var interval2 = retryPolicy.GetIntervalForNextAttempt(job);
        Assert.Equal(TimeSpan.FromSeconds(20), interval2);

        job = new JobExecutionModel { StartedCount = 3 };
        var interval3 = retryPolicy.GetIntervalForNextAttempt(job);
        Assert.Equal(TimeSpan.FromSeconds(20), interval3);

        job = new JobExecutionModel { StartedCount = 4 };
        var interval4 = retryPolicy.GetIntervalForNextAttempt(job);
        Assert.Null(interval4);
    }

    [Fact]
    public void JitterSpecified_ReturnsIntervalWithJitter()
    {
        var retryPolicy = new RetryPolicy
        {
            MaxCount = 4,
            IntervalsSeconds = [10, 20],
            JitterMaxValuesMs = [1000]
        };

        var job = new JobExecutionModel { StartedCount = 1 };
        var interval = retryPolicy.GetIntervalForNextAttempt(job);

        var intervalWithoutJitter = TimeSpan.FromSeconds(10);
        Assert.True(interval > intervalWithoutJitter);
    }

    [Fact]
    public void GetIntervalForNextAttempt_EmptyIntervals_ReturnsDefaultTenSeconds()
    {
        var retryPolicy = new RetryPolicy
        {
            MaxCount = 3,
            IntervalsSeconds = []
        };

        var firstJob = new JobExecutionModel { StartedCount = 0 };
        Assert.Equal(TimeSpan.FromSeconds(10), retryPolicy.GetIntervalForNextAttempt(firstJob));

        var secondJob = new JobExecutionModel { StartedCount = 2 };
        Assert.Equal(TimeSpan.FromSeconds(10), retryPolicy.GetIntervalForNextAttempt(secondJob));
    }

    [Fact]
    public void GetIntervalForNextAttempt_StartedCountExceedsIntervalsCount_ClampsToLastInterval()
    {
        var retryPolicy = new RetryPolicy
        {
            MaxCount = 10,
            IntervalsSeconds = [5, 10, 20]
        };

        var job = new JobExecutionModel { StartedCount = 4 };
        Assert.Equal(TimeSpan.FromSeconds(20), retryPolicy.GetIntervalForNextAttempt(job));

        job = new JobExecutionModel { StartedCount = 9 };
        Assert.Equal(TimeSpan.FromSeconds(20), retryPolicy.GetIntervalForNextAttempt(job));
    }

    [Fact]
    public void GetIntervalForNextAttempt_StartedCountEqualsMaxCount_ReturnsNull()
    {
        var retryPolicy = new RetryPolicy
        {
            MaxCount = 3,
            IntervalsSeconds = [10, 20]
        };

        var job = new JobExecutionModel { StartedCount = 3 };
        Assert.Null(retryPolicy.GetIntervalForNextAttempt(job));
    }

    [Fact]
    public void GetIntervalForNextAttempt_StartedCountGreaterThanMaxCount_ReturnsNull()
    {
        var retryPolicy = new RetryPolicy
        {
            MaxCount = 3,
            IntervalsSeconds = [10, 20]
        };

        var job = new JobExecutionModel { StartedCount = 10 };
        Assert.Null(retryPolicy.GetIntervalForNextAttempt(job));
    }

    [Fact]
    public void GetIntervalForNextAttempt_ShortJitterList_UsesLastJitterValue()
    {
        var retryPolicy = new RetryPolicy
        {
            MaxCount = 5,
            IntervalsSeconds = [10, 20, 30],
            JitterMaxValuesMs = [500]
        };

        var job = new JobExecutionModel { StartedCount = 3 };
        var interval = retryPolicy.GetIntervalForNextAttempt(job);

        Assert.NotNull(interval);
        Assert.InRange(interval.Value, TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(30500));
    }
}
