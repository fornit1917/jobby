using BenchmarkDotNet.Attributes;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.Postgres;
using Microsoft.Extensions.Logging;
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

        var loggerFactory = LoggerFactory.Create(x => x.AddConsole());
        var dataSource = DataSourceFactory.Create();
        var jobsStorage = new PgJobsStorage(dataSource);
        var serverSettings = new JobbyServerSettings
        {
            MaxDegreeOfParallelism = 10,
            PollingIntervalMs = 1000,
            UseBatches = true,
        };
        var jsonOptions = new JsonSerializerOptions();
        var serializer = new SystemTextJsonJobParamSerializer(jsonOptions);
        var scopeFactory = new JobbyTestExecutionScopeFactory();
        var retryPolicyService = new RetryPolicyService();
        var jobsRegistry = new JobsRegistryBuilder()
            .AddCommand<JobbyTestJobCommand, JobbyTestJobCommandHandler>()
            .Build();
        var jobbyServer = new JobbyServer(jobsStorage, scopeFactory, retryPolicyService, jobsRegistry, serializer, 
            loggerFactory.CreateLogger<JobbyServer>(), serverSettings);
        var jobsFactory = new JobsFactory(serializer);
        var jobsClient = new JobsClient(jobsFactory, jobsStorage);

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