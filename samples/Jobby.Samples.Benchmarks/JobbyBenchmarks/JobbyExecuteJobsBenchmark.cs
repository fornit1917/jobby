using BenchmarkDotNet.Attributes;
using Jobby.Abstractions.Models;
using Jobby.Core.Client;
using Jobby.Core.Server;
using Jobby.Postgres.CommonServices;
using System.Diagnostics;
using System.Text.Json;

namespace Jobby.Samples.Benchmarks.JobbyBenchmarks;

[MemoryDiagnoser]
public class JobbyExecuteJobsBenchmark : IBenchmark
{
    public string Name => "Jobby.Execute";

    public async Task Run()
    {
        var benchmarkParams = BenchamrkHelper.GetJobsBenchmarkParams(defaultJobsCount: 1000, 
            defaultJobName: TestJob.JobName,
            defaultJobDelayMs: 0);

        var dataSource = DataSourceFactory.Create();
        var jobsStorage = new PgJobsStorage(dataSource);
        var jobbySettings = new JobbySettings
        {
            MaxDegreeOfParallelism = 10,
            PollingIntervalMs = 1000,
            UseBatches = true,
        };
        var scopeFactory = new JobbyTestExecutionScopeFactory();
        var jobsServer = new JobsServer(jobsStorage, scopeFactory, jobbySettings);
        var jobsClient = new JobsClient(jobsStorage);

        Console.WriteLine("Clear jobs database");
        JobbyHelper.RemoveAllJobs(dataSource);

        Console.WriteLine($"Create {benchmarkParams.JobsCount} {benchmarkParams.JobName}");
        for (int i = 1; i <= benchmarkParams.JobsCount; i++)
        {
            var jobParam = new TestJobParam
            {
                Id = i,
                Value = Guid.NewGuid().ToString(),
                DelayMs = benchmarkParams.JobDelayMs,
            };
            var job = new JobModel
            {
                JobName = benchmarkParams.JobName,
                JobParam = JsonSerializer.Serialize(jobParam)
            };
            jobsClient.Enqueue(job);
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