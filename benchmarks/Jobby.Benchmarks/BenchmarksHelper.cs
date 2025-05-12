namespace Jobby.Benchmarks;

internal static class BenchmarksHelper
{
    public static BenchmarkParams GetCommonParams()
    {
        string? input;
        
        Console.Write("Degree of parallelism (default 10): ");
        input = Console.ReadLine();
        int degreeOfParallelism = string.IsNullOrWhiteSpace(input) ? 10 : int.Parse(input);

        return new BenchmarkParams
        {
            DegreeOfParallelism = degreeOfParallelism,
        };
    }

    public static BenchmarkParams GetJobbyParams()
    {
        string? input;

        var result = new BenchmarkParams();

        Console.Write("Degree of parallelism (default 10): ");
        input = Console.ReadLine();
        result.DegreeOfParallelism = string.IsNullOrWhiteSpace(input) ? 10 : int.Parse(input);

        Console.Write("Complete with batching (y/n, default n): ");
        input = Console.ReadLine()?.Trim();
        result.CompleteWithBatching = input == "y" || input == "Y";

        return result;
    }
}
