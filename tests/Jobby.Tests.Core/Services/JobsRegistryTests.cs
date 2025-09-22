using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.TestsUtils.Jobs;
using System.Collections.Frozen;

namespace Jobby.Tests.Core.Services;

public class JobsRegistryTests
{
    [Fact]
    public void GetJobExecutionMetadata_NotExistingJob_ReturnsNull()
    {
        var jobs = new Dictionary<string, IJobExecutorFactory>();
        var jobsRegistry = new JobsRegistry(jobs);

        var jobMetadata = jobsRegistry.GetJobExecutorFactory("not_existing");

        Assert.Null(jobMetadata);
    }

    [Fact]
    public void GetJobExecutionMetadata_ExistingJob_ReturnsJobMetadata()
    {
        var jobs = new Dictionary<string, IJobExecutorFactory>();
        var jobName = "jobName";
        var jobExecutorFactory = new JobExecutorFactory<TestJobCommand, TestJobCommandHandler>();
        jobs.Add(jobName, jobExecutorFactory);
        var jobsRegistry = new JobsRegistry(jobs.ToFrozenDictionary());

        var actualJobMetadata = jobsRegistry.GetJobExecutorFactory(jobName);
        Assert.Equal(jobExecutorFactory, actualJobMetadata);
    }
}
