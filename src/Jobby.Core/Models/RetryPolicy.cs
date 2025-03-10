﻿namespace Jobby.Core.Models;

public class RetryPolicy
{
    public static readonly RetryPolicy NoRetry = new RetryPolicy { MaxCount = 0, IntervalsSeconds = [] };

    public int MaxCount { get; init; }
    public IReadOnlyList<int> IntervalsSeconds { get; init; } = Array.Empty<int>(); // todo: change to ms

    public TimeSpan? GetIntervalForNextAttempt(Job job)
    {
        if (job.StartedCount >= MaxCount)
        {
            return null;
        }

        var intervalSeconds = 10;
        var intervalIndex = job.StartedCount > 0 ? job.StartedCount - 1 : 0;
        if (IntervalsSeconds.Count > 0)
        {
            intervalSeconds = IntervalsSeconds.Count > intervalIndex
                ? IntervalsSeconds[intervalIndex]
                : IntervalsSeconds[IntervalsSeconds.Count - 1];
        }

        return TimeSpan.FromSeconds(intervalSeconds);
    }

    public bool IsLastAttempt(Job job)
    {
        return job.StartedCount >= MaxCount;
    }
}
