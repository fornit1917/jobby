using System.Diagnostics;

namespace Jobby.TestsUtils.Tracing;

/// <summary>
/// Helpers to emulate the ambient (current) Activity in tests and to build the W3C
/// traceparent exactly the way <c>JobsFactory</c> captures it.
/// Prefer <see cref="BeginAmbientActivity"/> over <see cref="StartAmbientActivity"/>:
/// its returned scope resets <see cref="Activity.Current"/> to null on dispose,
/// so the reset cannot be forgotten.
/// </summary>
public static class TestTracingContext
{
    /// <summary>
    /// Starts an ambient Activity (it becomes <see cref="Activity.Current"/>).
    /// Tests that use it must reset <see cref="Activity.Current"/> to null in a finally block.
    /// </summary>
    public static Activity StartAmbientActivity(string name = "test-activity")
    {
        return new Activity(name).Start();
    }

    /// <summary>
    /// Starts an ambient Activity and returns a scope that resets
    /// <see cref="Activity.Current"/> to null on dispose.
    /// </summary>
    /// <param name="name">Activity name.</param>
    /// <param name="idFormat">ID format; use <see cref="ActivityIdFormat.Hierarchical"/> to emulate legacy apps.</param>
    /// <param name="traceFlags">Sampling flags; pass <see cref="ActivityTraceFlags.None"/> to emulate an unsampled request.
    /// When null, flags are left as .NET sets them on start.</param>
    public static AmbientActivityScope BeginAmbientActivity(string name = "test-activity",
        ActivityIdFormat idFormat = ActivityIdFormat.W3C,
        ActivityTraceFlags? traceFlags = null)
    {
        var activity = new Activity(name);
        activity.SetIdFormat(idFormat);
        if (traceFlags.HasValue)
        {
            activity.ActivityTraceFlags = traceFlags.Value;
        }
        activity.Start();
        return new AmbientActivityScope(activity);
    }

    /// <summary>
    /// Builds the W3C traceparent ("00-{trace-id}-{span-id}-{flags}") of an activity.
    /// </summary>
    public static string ToTraceparent(Activity activity)
    {
        return $"00-{activity.TraceId.ToHexString()}-{activity.SpanId.ToHexString()}-{((int)activity.ActivityTraceFlags).ToString("x2")}";
    }
}

/// <summary>
/// Disposable wrapper around an ambient Activity started by
/// <see cref="TestTracingContext.BeginAmbientActivity"/>; disposing resets
/// <see cref="Activity.Current"/> to null (as tests must do in a finally block).
/// </summary>
public sealed class AmbientActivityScope : IDisposable
{
    public AmbientActivityScope(Activity activity)
    {
        Activity = activity;
    }

    public Activity Activity { get; }

    /// <summary>
    /// The W3C traceparent of the wrapped activity, in the same format JobsFactory captures it.
    /// </summary>
    public string Traceparent => TestTracingContext.ToTraceparent(Activity);

    public void Dispose()
    {
        Activity.Current = null;
    }
}
