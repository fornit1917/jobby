using BenchmarkDotNet.Attributes;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.Postgres;
using System.Diagnostics;
using System.Text.Json;

namespace Jobby.Samples.Benchmarks.JobbyBenchmarks;

[MemoryDiagnoser]
public class JobbyExecuteJobsBenchmark : IBenchmark
{
    public string Name => "Jobby.Execute";

    public async Task Run()
    {
        var benchmarkParams = BenchamrkHelper.GetJobsBenchmarkParams(defaultJobsCount: 1000, defaultJobDelayMs: 0);

        var dataSource = DataSourceFactory.Create();
        var jobsStorage = new PgJobsStorage(dataSource);
        var jobbySettings = new JobbySettings
        {
            MaxDegreeOfParallelism = 10,
            PollingIntervalMs = 1000,
            UseBatches = true,
        };
        var jsonOptions = new JsonSerializerOptions();
        var serializer = new SystemTextJsonJobParamSerializer(jsonOptions);
        var scopeFactory = new JobbyTestExecutionScopeFactory(serializer);
        var retryPolicyService = new RetryPolicyService();
        var jobsServer = new JobsServer(jobsStorage, scopeFactory, retryPolicyService, jobbySettings);
        var jobsClient = new JobsClient(jobsStorage, serializer);

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
        jobsServer.StartBackgroundService();
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