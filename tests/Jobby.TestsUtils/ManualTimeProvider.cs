namespace Jobby.TestsUtils;

/// <summary>A monotonic clock that only moves when a test advances it.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private long _timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => Interlocked.Read(ref _timestamp);

    public void Advance(TimeSpan by) => Interlocked.Add(ref _timestamp, by.Ticks);
}
