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
        _factoryMock.Setup(x => x.Create(command, default(JobOpts))).Returns(job);

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
        _factoryMock.Setup(x => x.Create(firstCommand, default(JobOpts))).Returns(firstJob);
        _factoryMock.Setup(x => x.Create(secondCommand, default(JobOpts))).Returns(secondJob);

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
        _factoryMock
            .Setup(x => x.Create(command, It.Is<JobOpts>(opts => opts.StartTime == startTime)))
            .Returns(job);

        _builder.Add(command, startTime);
        var jobs = _builder.GetJobs();

        Assert.Single(jobs);
        Assert.Equal(job, jobs[0]);
    }

    [Fact]
    public void Add_CreationOptionsSpecified_GetJobsReturnsExpectedJob()
    {
        var command = new TestJobCommand();
        var creationOptions = new JobOpts
        {
            StartTime = DateTime.UtcNow.AddDays(1),
            QueueName = "custom-queue"
        };
        var job = new JobCreationModel();
        _factoryMock
            .Setup(x => x.Create(command, creationOptions))
            .Returns(job);

        _builder.Add(command, creationOptions);

        var jobs = _builder.GetJobs();

        Assert.Single(jobs);
        Assert.Equal(job, jobs[0]);
    }

    [Fact]
    public void GetJobs_CapacityConstructor_ChainOfThree_CorrectStatusesAndLinks()
    {
        var factory = new JobbyBuilder().CreateJobsFactory();
        var builder = new JobsSequenceBuilder(capacity: 10, factory);

        builder.Add(new TestJobCommand());
        builder.Add(new TestJobCommand());
        builder.Add(new TestJobCommand());
        var jobs = builder.GetJobs();

        Assert.Equal(3, jobs.Count);

        // first job is ready to start and points to the second one
        Assert.Equal(JobStatus.Scheduled, jobs[0].Status);
        Assert.Equal(jobs[1].Id, jobs[0].NextJobId);

        // middle job waits for the first one and points to the last one
        Assert.Equal(JobStatus.WaitingPrev, jobs[1].Status);
        Assert.Equal(jobs[2].Id, jobs[1].NextJobId);

        // last job waits for the middle one and has no next
        Assert.Equal(JobStatus.WaitingPrev, jobs[2].Status);
        Assert.Null(jobs[2].NextJobId);

        Assert.All(jobs.Select(x => x.Id), id => Assert.NotEqual(Guid.Empty, id));
        Assert.Distinct(jobs.Select(x => x.Id));
    }
}
