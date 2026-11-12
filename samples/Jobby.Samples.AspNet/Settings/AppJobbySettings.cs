using Jobby.Core.Models;

namespace Jobby.Samples.AspNet.Settings;

public class AppJobbySettings
{
    public bool UseMetrics { get; init; } = true;
    public bool UseTracing { get; init; }
    public JobbyTracingMode TracingMode { get; init; }
}
