using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Jobby.Abstractions.Client;
using Jobby.Abstractions.Models;
using Jobby.Core.Client;
using Jobby.Postgres.CommonServices;
using System.Text.Json;

namespace Jobby.Samples.Benchmarks.JobbyBenchmarks;

public class JobbyCreateJobsBenchmark : IBenchmark
{
    public string Name => "Jobby.Create.5";

    public Task Run()
    {
        BenchmarkRunner.Run<JobbyCreateJobsBenchmarkAction>();
        return Task.CompletedTask;
    }
}

[MemoryDiagnoser]
public class JobbyCreateJobsBenchmarkAction
{
    private readonly IJobsClient _jobsClient;

    public JobbyCreateJobsBenchmarkAction()
    {
        var dataSource = DataSourceFactory.Create();
        var jobsStorage = new PgJobsStorage(dataSource);
        _jobsClient = new JobsClient(jobsStorage);
    }

    [Benchmark]
    public async Task JobbyCreateJobs()
    {
        const int jobsCount = 5;
        for (int i = 1; i <= jobsCount; i++)
        {
            var jobParam = new TestJobParam
            {
                Id = i,
                Value = Guid.NewGuid().ToString(),
            };
            var job = new JobModel
            {
                JobName = "TestJob",
                JobParam = JsonSerializer.Serialize(jobParam)
            };
            await _jobsClient.EnqueueAsync(job);
        }
    }
}
