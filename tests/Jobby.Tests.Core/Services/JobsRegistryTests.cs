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
        var jobs = new Dictionary<string, JobExecutionMetadata>();
        var jobsRegistry = new JobsRegistry(jobs);

        var jobMetadata = jobsRegistry.GetJobExecutionMetadata("not_existing");

        Assert.Null(jobMetadata);
    }

    [Fact]
    public void GetJobExecutionMetadata_ExistingJob_ReturnsJobMetadata()
    {
        var jobs = new Dictionary<string, JobExecutionMetadata>();
        var jobName = "jobName";
        var jobMetadata = new JobExecutionMetadata
        {
            CommandType = typeof(TestJobCommand),
            HandlerType = typeof(IJobCommandHandler<TestJobCommand>),
            HandlerImplType = typeof(TestJobCommandHandler),
            ExecMethod = typeof(TestJobCommandHandler).GetMethod("ExecuteAsync") ?? throw new Exception("Method Execute not found")
        };
        jobs.Add(jobName, jobMetadata);
        var jobsRegistry = new JobsRegistry(jobs.ToFrozenDictionary());

        var actualJobMetadata = jobsRegistry.GetJobExecutionMetadata(jobName);
        Assert.Equal(jobMetadata, actualJobMetadata);
    }
}
