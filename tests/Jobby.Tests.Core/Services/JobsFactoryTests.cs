using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Queues;
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
    private readonly Mock<IQueueNameAssignor> _queueNameAssignorMock;

    private readonly JobsFactory _factory;

    public JobsFactoryTests()
    {
        _serializerMock = new Mock<IJobParamSerializer>();
        _queueNameAssignorMock = new Mock<IQueueNameAssignor>();
        var guidGeneratorMock = new Mock<IGuidGenerator>();
        guidGeneratorMock.Setup(x => x.NewGuid()).Returns(JobId);

        _factory = new JobsFactory(guidGeneratorMock.Object, _serializerMock.Object, _queueNameAssignorMock.Object);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_TimeNotSpecified_CreatesJobModelWithNowStartTime(bool canBeRestarted)
    {
        var command = new TestJobCommand { Restartable = canBeRestarted };
        _serializerMock.Setup(x => x.SerializeJobParam(command)).Returns(SerializedCommand);
        var expectedQueue = "q";
        _queueNameAssignorMock
            .Setup(x => x.GetQueueName(TestJobCommand.GetJobName(), default))
            .Returns(expectedQueue);

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
        Assert.Equal(expectedQueue, job.QueueName);
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
        var expectedQueue = "q";
        _queueNameAssignorMock
            .Setup(x => x.GetQueueName(TestJobCommand.GetJobName(), 
                It.Is<JobOpts>(opts => opts.StartTime == startTime)))
            .Returns(expectedQueue);

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
        Assert.Equal(expectedQueue, job.QueueName);
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
        var expectedQueue = "q";
        _queueNameAssignorMock
            .Setup(x => x.GetQueueNameForRecurrent(TestJobCommand.GetJobName(), default))
            .Returns(expectedQueue);

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
        Assert.Equal(expectedQueue, job.QueueName);
        Assert.Equal(canBeRestarted, job.CanBeRestarted);
    }

    [Fact]
    public void Create_CreationOptionsSpecified_CreatesExpected()
    {
        var command = new TestJobCommand();
        var creationOptions = new JobOpts
        {
            QueueName = "custom_q",
            StartTime = DateTime.UtcNow.AddDays(1),
        };
        _serializerMock.Setup(x => x.SerializeJobParam(command)).Returns(SerializedCommand);
        var expectedQueue = "q";
        _queueNameAssignorMock
            .Setup(x => x.GetQueueName(TestJobCommand.GetJobName(), creationOptions))
            .Returns(expectedQueue);
        
        var job = _factory.Create(command, creationOptions);

        Assert.Equal(JobId,  job.Id);
        Assert.Equal(TestJobCommand.GetJobName(), job.JobName);
        Assert.Equal(SerializedCommand, job.JobParam);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Null(job.Cron);
        Assert.True(DateTime.UtcNow >= job.CreatedAt);
        Assert.True(DateTime.UtcNow.Subtract(job.CreatedAt) < TimeSpan.FromSeconds(1));
        Assert.Equal(creationOptions.StartTime.Value, job.ScheduledStartAt);
        Assert.Equal(expectedQueue, job.QueueName);
        Assert.Null(job.NextJobId);
    }

    [Fact]
    public void CreateRecurrent_CreationOptionsSpecified_CreatesExpected()
    {
        var command = new TestJobCommand();
        var cron = "0 3 1 12 *";
        var creationOptions = new RecurrentJobOpts
        {
            QueueName = "custom_q",
            StartTime = DateTime.UtcNow.AddDays(1),
        };
        _serializerMock.Setup(x => x.SerializeJobParam(command)).Returns(SerializedCommand);
        var expectedQueue = "q";
        _queueNameAssignorMock
            .Setup(x => x.GetQueueNameForRecurrent(TestJobCommand.GetJobName(), creationOptions))
            .Returns(expectedQueue);

        var job = _factory.CreateRecurrent(command, cron, creationOptions);
        
        Assert.Equal(JobId,  job.Id);
        Assert.Equal(TestJobCommand.GetJobName(), job.JobName);
        Assert.Equal(SerializedCommand, job.JobParam);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Equal(cron, job.Cron);
        Assert.True(DateTime.UtcNow >= job.CreatedAt);
        Assert.True(DateTime.UtcNow.Subtract(job.CreatedAt) < TimeSpan.FromSeconds(1));
        Assert.Equal(creationOptions.StartTime.Value, job.ScheduledStartAt);
        Assert.Null(job.NextJobId);
        Assert.Equal(expectedQueue, job.QueueName);
    }
}
