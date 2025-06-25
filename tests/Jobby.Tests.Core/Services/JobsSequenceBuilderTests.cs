using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.TestsUtils.Jobs;
using Moq;

namespace Jobby.Tests.Core.Services;

public class JobsSequenceBuilderTests
{
    private readonly Mock<IJobsFactory> _factoryMock;

    private readonly JobsSequenceBuilder _builder;

    public JobsSequenceBuilderTests()
    {
        _factoryMock = new Mock<IJobsFactory>();
        _builder = new JobsSequenceBuilder(_factoryMock.Object);
    }

    [Fact]
    public void GetJobs_Empty_ReturnsEmptyList()
    {
        var jobs = _builder.GetJobs();
        Assert.Empty(jobs);
    }

    [Fact]
    public void GetJobs_Single_ReturnsSingleJobReadyToStart()
    {
        var command = new TestJobCommand();
        var job = new JobCreationModel
        {
            Status = JobStatus.Scheduled
        };
        _factoryMock.Setup(x => x.Create(command, It.IsAny<DateTime>())).Returns(job);

        _builder.Add(command);
        var jobs = _builder.GetJobs();

        Assert.Single(jobs);
        Assert.Equal(job, jobs[0]);
        Assert.Equal(JobStatus.Scheduled, jobs[0].Status);
        Assert.Null(jobs[0].NextJobId);
    }

    [Fact]
    public void GetJobs_Two_ReturnsTwoReadtFirstWithNextWaitingSecond()
    {
        var firstCommand = new TestJobCommand();
        var secondCommand = new TestJobCommand();
        var firstJob = new JobCreationModel
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Scheduled
        };
        var secondJob = new JobCreationModel
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Scheduled
        };
        _factoryMock.Setup(x => x.Create(firstCommand, It.IsAny<DateTime>())).Returns(firstJob);
        _factoryMock.Setup(x => x.Create(secondCommand, It.IsAny<DateTime>())).Returns(secondJob);

        _builder.Add(firstCommand);
        _builder.Add(secondCommand);
        var jobs = _builder.GetJobs();

        Assert.Equal(2, jobs.Count);
        Assert.Equal(firstJob, jobs[0]);
        Assert.Equal(secondJob, jobs[1]);
        Assert.Equal(JobStatus.Scheduled, jobs[0].Status);
        Assert.Equal(secondJob.Id, jobs[0].NextJobId);
        Assert.Equal(JobStatus.WaitingPrev, jobs[1].Status);
        Assert.Null(jobs[1].NextJobId);
    }

    [Fact]
    public void GetJobs_StartTimeSpecified_CreatesJobWithSpecifiedStartTime()
    {
        var command = new TestJobCommand();
        var startTime = DateTime.UtcNow.AddDays(1);
        var job = new JobCreationModel
        {
            Status = JobStatus.Scheduled
        };
        _factoryMock.Setup(x => x.Create(command, startTime)).Returns(job);

        _builder.Add(command, startTime);
        var jobs = _builder.GetJobs();

        Assert.Single(jobs);
        Assert.Equal(job, jobs[0]);
    }
}
