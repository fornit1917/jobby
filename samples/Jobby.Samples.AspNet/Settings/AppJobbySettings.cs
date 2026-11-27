using Jobby.Core.Models;

namespace Jobby.Samples.AspNet.Settings;

public class AppJobbyQueueSettings
{
    public string QueueName { get; init; } = QueueSettings.DefaultQueueName;
    public int MaxDegreeOfParallelism { get; init; }
    public bool? DisableSerializableGroups { get; init; }
}

public class AppJobbySettings
{
    public bool UseMetrics { get; init; } = true;
    public bool UseTracing { get; init; }
    public JobbyTracingMode TracingMode { get; init; }
    public bool SeedDashboardDemoData { get; init; }

    // Jobby server settings bound from the "Jobby:Server" appsettings section,
    // so they can be changed without rebuilding (queues are configured in code).
    public JobbyServerSettings Server { get; init; } = new();

    // Queues bound from the "Jobby:Queues" appsettings section.
    // MaxDegreeOfParallelism=0 means "use the server-level default" (QueueSettings semantics).
    public AppJobbyQueueSettings[] Queues { get; init; } = [];
}
