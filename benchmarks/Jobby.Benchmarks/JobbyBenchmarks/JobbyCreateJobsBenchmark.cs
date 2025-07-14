using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Jobby.Core;
using Jobby.Core.Interfaces;
using Jobby.Core.Services;
using Jobby.Postgres.ConfigurationExtensions;

namespace Jobby.Benchmarks.JobbyBenchmarks;

public class JobbyCreateJobsBenchmark : IBenchmark
{
    public string Name => "Jobby.Create.1";

    public Task Run()
    {
        BenchmarkRunner.Run<JobbyCreateJobsBenchmarkAction>();
        return Task.CompletedTask;
    }
}

[MemoryDiagnoser]
[WarmupCount(10)]
[IterationCount(10)]
public class JobbyCreateJobsBenchmarkAction
{
    private readonly IJobbyClient _jobbyClient;

    public JobbyCreateJobsBenchmarkAction()
    {
        var dataSource = DataSourceFactory.Create();
        var builder = new JobbyServicesBuilder();
        builder.UsePostgresql(dataSource);
        _jobbyClient = builder.CreateJobbyClient();
    }

    [Benchmark]
    public async Task JobbyCreateJob()
    {
        var jobCommand = new JobbyTestJobCommand
        {
            Id = 1,
            Value = Guid.NewGuid().ToString(),
            DelayMs = 0,
        };
        await _jobbyClient.EnqueueCommandAsync(jobCommand);
    }
}
