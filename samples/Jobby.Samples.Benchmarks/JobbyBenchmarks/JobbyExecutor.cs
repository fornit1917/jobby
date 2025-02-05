using System.Text.Json;
using Jobby.Abstractions.Models;
using Jobby.Abstractions.Server;

namespace Jobby.Samples.Benchmarks.JobbyBenchmarks;

public class JobbyTestExecutor : IJobExecutor
{
    public Task ExecuteAsync(JobModel job)
    {
        if (job.JobName == TestJob.JobName)
        {
            var jobHandler = new TestJob();
            var jobParam = job.JobParam != null 
                ? JsonSerializer.Deserialize<TestJobParam>(job.JobParam)
                : null;
            return jobHandler.Execute(jobParam);
        }
        return Task.CompletedTask;
    }
}

public class JobbyTestExecutionScope : IJobExecutionScope
{
    private readonly IJobExecutor _executor;

    public JobbyTestExecutionScope(IJobExecutor executor)
    {
        _executor = executor;
    }

    public void Dispose()
    {
    }

    public IJobExecutor GetJobExecutor(string jobName)
    {
        return _executor;
    }
}

public class JobbyTestExecutionScopeFactory : IJobExecutionScopeFactory
{
    private readonly IJobExecutor _executor = new JobbyTestExecutor();

    public IJobExecutionScope CreateJobExecutionScope()
    {
        return new JobbyTestExecutionScope(_executor);
    }
}