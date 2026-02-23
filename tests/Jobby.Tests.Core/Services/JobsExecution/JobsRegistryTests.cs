using System.Collections.Frozen;
using Jobby.Core.Interfaces.ServerModules.JobsExecution;
using Jobby.Core.Services.ServerModules.JobsExecution;
using Jobby.TestsUtils.Jobs;

namespace Jobby.Tests.Core.Services.JobsExecution;

public class JobsRegistryTests
{
    [Fact]
    public void GetJobExecutionMetadata_NotExistingJob_ReturnsNull()
    {
        var jobs = new Dictionary<string, IJobExecutor>();
        var jobsRegistry = new JobsRegistry(jobs);

        var jobMetadata = jobsRegistry.GetJobExecutor("not_existing");

        Assert.Null(jobMetadata);
    }

    [Fact]
    public void GetJobExecutionMetadata_ExistingJob_ReturnsJobMetadata()
    {
        var jobs = new Dictionary<string, IJobExecutor>();
        var jobName = "jobName";
        var jobExecutorFactory = new JobExecutor<TestJobCommand, TestJobCommandHandler>();
        jobs.Add(jobName, jobExecutorFactory);
        var jobsRegistry = new JobsRegistry(jobs.ToFrozenDictionary());

        var actualJobMetadata = jobsRegistry.GetJobExecutor(jobName);
        Assert.Equal(jobExecutorFactory, actualJobMetadata);
    }
}
