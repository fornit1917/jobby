namespace Jobby.Abstractions.Models;

public class JobbySettings
{
    public int PollingIntervalMs { get; set; } = 1000;
    public int DbErrorPauseMs { get; set; } = 5000;
    public int MaxDegreeOfParallelism { get; set; } = 10;
}
