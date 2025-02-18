using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Jobby.Core.Interfaces;
using Jobby.Core.Services;
using Jobby.Postgres;
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
    private readonly IJobsMediator _jobsClient;

    public JobbyCreateJobsBenchmarkAction()
    {
        var dataSource = DataSourceFactory.Create();
        var jobsStorage = new PgJobsStorage(dataSource);
        var jsonOptions = new JsonSerializerOptions();
        var serializer = new SystemTextJsonJobParamSerializer(jsonOptions);
        _jobsClient = new JobsClient(jobsStorage, serializer);
    }

    [Benchmark]
    public async Task JobbyCreateJobs()
    {
        const int jobsCount = 5;
        for (int i = 1; i <= jobsCount; i++)
        {
            var jobCommand = new JobbyTestJobCommand
            {
                Id = i,
                Value = Guid.NewGuid().ToString(),
                DelayMs = 0,
            };

            await _jobsClient.EnqueueCommandAsync(jobCommand);
        }
    }
}
