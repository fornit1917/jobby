namespace Jobby.Core.Models;

/// <summary>
/// Defines how a job execution Activity relates to the trace of the code that created the job.
/// </summary>
public enum JobbyTracingMode
{
    /// <summary>
    /// Job execution always starts a new trace with a new TraceId. Default behaviour.
    /// </summary>
    CreateNewTraceId = 0,

    /// <summary>
    /// The TraceId of the current Activity is captured at job creation time and
    /// the job execution Activity continues that trace (the captured traceparent
    /// becomes the parent context of the job span).
    /// </summary>
    RestoreTraceId = 1,

    /// <summary>
    /// The TraceId of the current Activity is captured at job creation time, but
    /// the job execution Activity starts a new trace and references the captured
    /// trace via an ActivityLink instead of a parent-child relationship.
    /// </summary>
    LinkTraceId = 2,
}
