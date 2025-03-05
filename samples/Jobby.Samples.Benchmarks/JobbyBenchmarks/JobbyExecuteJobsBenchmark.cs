using BenchmarkDotNet.Attributes;
using Jobby.Core.Models;
using Jobby.Core.Services.Builders;
using Jobby.Postgres.ConfigurationExtensions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Jobby.Samples.Benchmarks.JobbyBenchmarks;

[MemoryDiagnoser]
public class JobbyExecuteJobsBenchmark : IBenchmark
{
    public string Name => "Jobby.Execute";

    public async Task Run()
    {
        var benchmarkParams = BenchamrkHelper.GetJobsBenchmarkParams(defaultJobsCount: 1000, defaultJobDelayMs: 0);

        var loggerFactory = LoggerFactory.Create(x => x.AddConsole());
        var dataSource = DataSourceFactory.Create();
        var serverSettings = new JobbyServerSettings
        {
            MaxDegreeOfParallelism = 10,
            PollingIntervalMs = 1000,
            UseBatches = true,
        };
        var scopeFactory = new JobbyTestExecutionScopeFactory();

        var builder = new JobbyServicesBuilder();
        builder
            .UsePostgresql(dataSource)
            .UseExecutionScopeFactory(scopeFactory)
            .UseJobs(x => x.AddCommand<JobbyTestJobCommand, JobbyTestJobCommandHandler>());

        var jobbyServer = builder.CreateJobbyServer();
        var jobsClient = builder.CreateJobsClient();

        Console.WriteLine("Clear jobs database");
        JobbyHelper.RemoveAllJobs(dataSource);

        Console.WriteLine($"Create {benchmarkParams.JobsCount} jobs");
        for (int i = 1; i <= benchmarkParams.JobsCount; i++)
        {
            var jobCommand = new JobbyTestJobCommand
            {
                Id = i,
                Value = Guid.NewGuid().ToString(),
                DelayMs = 0,
            };
            jobsClient.EnqueueCommand(jobCommand);
        }

        Console.WriteLine("Start jobby server");
        
        Stopwatch stopwatch = Stopwatch.StartNew();
        jobbyServer.StartBackgroundService();
        var hasNotCompletedJobs = true;
        while (hasNotCompletedJobs)
        {
            hasNotCompletedJobs = JobbyHelper.HasNotCompletedJobs(dataSource);
            if (hasNotCompletedJobs)
            {
                await Task.Delay(100);
            }
        }
        stopwatch.Stop();

        Console.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds} ms");
    }
}