namespace Jobby.Samples.Benchmarks;

public static class BenchamrkHelper
{
    public static JobsBenchmarkParams GetJobsBenchmarkParams(int defaultJobsCount, string defaultJobName, int defaultJobDelayMs)
    {
        string? input;

        Console.Write($"Jobs count (default {defaultJobsCount}): ");
        input = Console.ReadLine();
        int jobsCount = string.IsNullOrWhiteSpace(input) ? defaultJobsCount : int.Parse(input);

        Console.Write($"Job name (default {defaultJobName}): ");
        input = Console.ReadLine();
        string jobName = string.IsNullOrWhiteSpace(input) ? defaultJobName : input;

        Console.Write($"Job delay, ms (default {defaultJobDelayMs}): ");
        input = Console.ReadLine();
        int jobDelay = string.IsNullOrWhiteSpace(input) ? defaultJobDelayMs : int.Parse(input);

        return new JobsBenchmarkParams
        {
            JobName = jobName,
            JobsCount = jobsCount,
            JobDelayMs = jobDelay
        };
    }
}
