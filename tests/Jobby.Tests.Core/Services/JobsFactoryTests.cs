using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.TestsUtils.Jobs;
using Moq;

namespace Jobby.Tests.Core.Services;

public class JobsFactoryTests
{
    private const string SerializedCommand = "serializedParam";
    private static readonly Guid JobId = Guid.NewGuid();

    private readonly Mock<IJobParamSerializer> _serializerMock;

    private readonly JobsFactory _factory;

    public JobsFactoryTests()
    {
        _serializerMock = new Mock<IJobParamSerializer>();
        var guidGeneratorMock = new Mock<IGuidGenerator>();
        guidGeneratorMock.Setup(x => x.NewGuid()).Returns(JobId);

        _factory = new JobsFactory(guidGeneratorMock.Object, _serializerMock.Object);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_TimeNotSpecified_CreatesJobModelWithNowStartTime(bool canBeRestarted)
    {
        var command = new TestJobCommand { Restartable = canBeRestarted };
        _serializerMock.Setup(x => x.SerializeJobParam(command)).Returns(SerializedCommand);

        var job = _factory.Create(command);

        Assert.Equal(JobId,  job.Id);
        Assert.Equal(TestJobCommand.GetJobName(), job.JobName);
        Assert.Equal(SerializedCommand, job.JobParam);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Null(job.Cron);
        Assert.True(DateTime.UtcNow >= job.CreatedAt);
        Assert.True(DateTime.UtcNow.Subtract(job.CreatedAt) < TimeSpan.FromSeconds(1));
        Assert.True(DateTime.UtcNow >= job.ScheduledStartAt);
        Assert.True(DateTime.UtcNow.Subtract(job.ScheduledStartAt) < TimeSpan.FromSeconds(1));
        Assert.Null(job.NextJobId);
        Assert.Equal(canBeRestarted, job.CanBeRestarted);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_TimeSpecified_CreatesJobModelWithSpecifiedStartTime(bool canBeRestarted)
    {
        var command = new TestJobCommand { Restartable = canBeRestarted };
        var startTime = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        _serializerMock.Setup(x => x.SerializeJobParam(command)).Returns(SerializedCommand);

        var job = _factory.Create(command, startTime);

        Assert.Equal(JobId,  job.Id);
        Assert.Equal(TestJobCommand.GetJobName(), job.JobName);
        Assert.Equal(SerializedCommand, job.JobParam);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Null(job.Cron);
        Assert.True(DateTime.UtcNow >= job.CreatedAt);
        Assert.True(DateTime.UtcNow.Subtract(job.CreatedAt) < TimeSpan.FromSeconds(1));
        Assert.Equal(startTime, job.ScheduledStartAt);
        Assert.Null(job.NextJobId);
        Assert.Equal(canBeRestarted, job.CanBeRestarted);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CreateRecurrent_CreatesJobModelWithSpecifiedCronAndCalculatedStartTime(bool canBeRestarted)
    {
        var command = new TestJobCommand { Restartable = canBeRestarted };
        _serializerMock.Setup(x => x.SerializeJobParam(command)).Returns(SerializedCommand);
        var cron = "0 3 1 12 *";

        var job = _factory.CreateRecurrent(command, cron);

        Assert.Equal(JobId,  job.Id);
        Assert.Equal(TestJobCommand.GetJobName(), job.JobName);
        Assert.Equal(SerializedCommand, job.JobParam);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Equal(cron, job.Cron);
        Assert.True(DateTime.UtcNow >= job.CreatedAt);
        Assert.True(DateTime.UtcNow.Subtract(job.CreatedAt) < TimeSpan.FromSeconds(1));
        var expectedStartTime = CronHelper.GetNext(cron, job.CreatedAt);
        Assert.Equal(expectedStartTime, job.ScheduledStartAt);
        Assert.Null(job.NextJobId);
        Assert.Equal(canBeRestarted, job.CanBeRestarted);
    }
}
