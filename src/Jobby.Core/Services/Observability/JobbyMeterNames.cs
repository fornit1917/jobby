namespace Jobby.Core.Services.Observability;

public static class JobbyMeterNames
{
    public const string JobsExecution = "Jobby.JobsExecution";

    public static string[] GetAll() => [JobsExecution];

    internal static readonly string? Version = typeof(JobbyMeterNames).Assembly.GetName().Version?.ToString();
}
