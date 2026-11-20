namespace Jobby.Core.Models;

public class JobbyTracingSettings
{
    /// <summary>
    /// Defines how job execution Activities relate to the trace active at job creation time.
    /// <see cref="JobbyTracingMode.CreateNewTraceId"/> (default) — every job execution starts a new trace.
    /// <see cref="JobbyTracingMode.RestoreTraceId"/> — job execution continues the trace captured at creation time.
    /// <see cref="JobbyTracingMode.LinkTraceId"/> — job execution starts a new trace linked to the captured one.
    /// Recurrent jobs always start a new trace. Can be overridden per job via <see cref="JobOpts.TracingMode"/>.
    /// </summary>
    public JobbyTracingMode TracingMode { get; init; } = JobbyTracingMode.CreateNewTraceId;
}
