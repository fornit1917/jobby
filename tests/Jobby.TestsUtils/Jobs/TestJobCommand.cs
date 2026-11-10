using Jobby.Core.Interfaces;

namespace Jobby.TestsUtils.Jobs;

public class TestJobCommand : IJobCommand
{
    public Guid UniqueId { get; init; } = Guid.NewGuid();
    public Exception? ExceptionToThrow { get; init; }

    /// <summary>
    /// When greater than zero, the handler emulates work by delaying for this amount of milliseconds.
    /// Concurrency of such slow executions is tracked by <see cref="SlowJobTracker"/>.
    /// </summary>
    public int DelayMs { get; init; }

    public static string GetJobName() => "TestJob";
}

/// <summary>
/// Tracks concurrency of slow (DelayMs > 0) TestJobCommand executions.
/// </summary>
public static class SlowJobTracker
{
    private static int _current;
    private static int _max;

    public static int MaxConcurrent => _max;

    public static void Reset()
    {
        // a slow handler from a previous test may still be in flight; wait for it,
        // otherwise its Exit() would decrement the counter past zero and the new
        // test's Enter() calls would never raise _max
        SpinWait.SpinUntil(() => Volatile.Read(ref _current) <= 0, TimeSpan.FromSeconds(30));
        _current = 0;
        _max = 0;
    }

    public static void Enter()
    {
        var current = Interlocked.Increment(ref _current);
        var max = _max;
        while (current > max && Interlocked.CompareExchange(ref _max, current, max) != max)
        {
            max = _max;
        }
    }

    public static void Exit()
    {
        Interlocked.Decrement(ref _current);
    }
}
