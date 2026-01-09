namespace Jobby.Benchmarks;

internal static class BenchmarksHelper
{
    public static BenchmarkParams GetCommonParams()
    {
        string? input;

        var result = new BenchmarkParams();

        Console.Write("Jobs count (default 1000): ");
        input = Console.ReadLine();
        result.JobsCount = string.IsNullOrWhiteSpace(input) ? 1000 : int.Parse(input);

        Console.Write("Degree of parallelism (default 10): ");
        input = Console.ReadLine();
        result.DegreeOfParallelism = string.IsNullOrWhiteSpace(input) ? 10 : int.Parse(input);

        return result;
    }

    public static BenchmarkParams GetJobbyParams()
    {
        string? input;

        var result = GetCommonParams();

        Console.Write("Complete with batching (y/n, default n): ");
        input = Console.ReadLine()?.Trim();
        result.CompleteWithBatching = input == "y" || input == "Y";
        
        Console.Write("Use UuidV7 (y/n, default y): ");
        input = Console.ReadLine()?.Trim();
        result.UseUuidV7 = input == "y" || input == "Y";

        return result;
    }
}
