namespace Jobby.Core.Models;

public class JobbyServerSettings
{
    public int PollingIntervalMs { get; set; } = 1000;
    public int DbErrorPauseMs { get; set; } = 5000;
    public int MaxDegreeOfParallelism { get; set; } = 10;
    public bool UseBatches { get; set; } = false;
    public bool DeleteCompleted { get; set; } = true;
}
