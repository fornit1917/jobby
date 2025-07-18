﻿using Jobby.Benchmarks.HangfireBenchmarks;
using Jobby.Benchmarks.JobbyBenchmarks;
using Jobby.Benchmarks.QuartzBenchmarks;

namespace Jobby.Benchmarks
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var benchmarks = new IBenchmark[]
            {
                new JobbyCreateJobsBenchmark(),
                new JobbyBulkCreateJobsBenchmark(),
                new JobbyExecuteJobsBenchmark(useBenchmarkLib: true),
                new JobbyExecuteJobsBenchmark(useBenchmarkLib: false),

                new HangfireCreateJobsBenchmark(),
                new HangfireBulkCreateJobsBenchmark(),
                new HangfireExecuteJobsBenchmark(useBenchmarkLib: true),
                new HangfireExecuteJobsBenchmark(useBenchmarkLib: false),

                new QuartzCreateJobsBenchmark(),
                new QuartzBulkCreateJobsBenchmark(),
                new QuartzExecuteJobsBenchmark(useBenchamrkLib: true),
                new QuartzExecuteJobsBenchmark(useBenchamrkLib: false),
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
