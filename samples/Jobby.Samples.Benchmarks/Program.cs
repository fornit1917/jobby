using BenchmarkDotNet.Running;
using Jobby.Samples.Benchmarks.HangfireBenchmarks;
using Jobby.Samples.Benchmarks.JobbyBenchmarks;
using Jobby.Samples.Benchmarks.QuartzBenchmarks;

namespace Jobby.Samples.Benchmarks
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var benchmarks = new IBenchmark[]
            {
                new JobbyCreateJobsBenchmark(),
                new JobbyExecuteJobsBenchmark(),
                new HangfireCreateJobsBenchmark(),
                new HangfireExecuteJobsBenchmark(),
                new QuartzCreateJobsBenchmark(),
                new QuartzExecuteJobsBenchmark(),
            };

            for (int i = 0; i < benchmarks.Length; i++)
            {
                var benchmark = benchmarks[i];
                Console.WriteLine($"{i+1}. {benchmark.Name}");
            }

            Console.Write("Benchmark number: ");
            var input = Console.ReadLine();
            int.TryParse(input, out var benchmarkNumber);

            if (benchmarkNumber < 1 || benchmarkNumber > benchmarks.Length)
            {
                Console.WriteLine("Invalid benchmark number");
                return;
            }

            await benchmarks[benchmarkNumber-1].Run();
        }
    }
}
