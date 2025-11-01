using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.Postgres.ConfigurationExtensions;

namespace Jobby.Benchmarks.JobbyBenchmarks;

public class JobbyBulkCreateJobsBenchmark : IBenchmark
{
    public string Name => "Jobby.BulkCreate.10";

    public Task Run()
    {
        BenchmarkRunner.Run<JobbyBulkCreateJobsBenchmarkAction>();
        return Task.CompletedTask;
    }
}

[MemoryDiagnoser]
[WarmupCount(10)]
[IterationCount(10)]
public class JobbyBulkCreateJobsBenchmarkAction
{
    private readonly IJobbyClient _jobbyClient;

    public JobbyBulkCreateJobsBenchmarkAction()
    {
        var dataSource = DataSourceFactory.Create();
        var builder = new JobbyBuilder();
        builder.UsePostgresql(dataSource);
        _jobbyClient = builder.CreateJobbyClient();
    }

    [Benchmark]
    public async Task JobbyBulkCreateJobs()
    {
        const int jobsCount = 10;
        var jobs = new List<JobCreationModel>(capacity: jobsCount);
        for (int i = 1; i <= jobsCount; i++)
        {
            var jobCommand = new JobbyTestJobCommand
            {
                Id = i,
                Value = Guid.NewGuid().ToString(),
                DelayMs = 0,
            };
            jobs.Add(_jobbyClient.Factory.Create(jobCommand));
        }
        await _jobbyClient.EnqueueBatchAsync(jobs);
    }
}