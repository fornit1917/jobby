using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Jobby.Core.Interfaces;
using Jobby.Core.Services.Builders;
using Jobby.Postgres.ConfigurationExtensions;

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
        var builder = new JobbyServicesBuilder();
        builder.UsePostgresql(dataSource);
        _jobsClient = builder.CreateJobsClient();
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
