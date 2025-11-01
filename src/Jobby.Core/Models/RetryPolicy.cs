namespace Jobby.Core.Models;

public class RetryPolicy
{
    public static readonly RetryPolicy NoRetry = new RetryPolicy { MaxCount = 0, IntervalsSeconds = [] };

    public int MaxCount { get; init; }
    public IReadOnlyList<int> IntervalsSeconds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> JitterMaxValuesMs { get; init; } = Array.Empty<int>();

    public TimeSpan? GetIntervalForNextAttempt(JobExecutionModel job)
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

        if (JitterMaxValuesMs.Count > 0)
        {
            var jitterMaxMs = JitterMaxValuesMs.Count > intervalIndex
                ? JitterMaxValuesMs[intervalIndex]
                : JitterMaxValuesMs[JitterMaxValuesMs.Count - 1];
            var jitterMs = Random.Shared.Next(jitterMaxMs + 1);

            return TimeSpan.FromMilliseconds(intervalSeconds * 1000 + jitterMs);
        }

        return TimeSpan.FromSeconds(intervalSeconds);
    }

    public bool IsLastAttempt(JobExecutionModel job)
    {
        return job.StartedCount >= MaxCount;
    }
}
