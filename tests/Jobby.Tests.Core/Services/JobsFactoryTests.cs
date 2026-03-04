using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.Core.Services.Schedulers;
using Jobby.TestsUtils.Jobs;
using Moq;

namespace Jobby.Tests.Core.Services;

public class JobsFactoryTests
{
    private const string SerializedCommand = "serializedParam";
    private static readonly Guid JobId = Guid.NewGuid();
    private readonly Mock<IGuidGenerator> _guidGeneratorMock;

    private readonly Mock<IJobParamSerializer> _serializerMock;

    public JobsFactoryTests()
    {
        _serializerMock = new Mock<IJobParamSerializer>();
        _guidGeneratorMock = new Mock<IGuidGenerator>();
        _guidGeneratorMock.Setup(x => x.NewGuid()).Returns(JobId);
    }

    [Fact]
    public void Create_OptionsNotSpecified_CreatesJobModelWithDefaultOptions()
    {
        var command = new TestJobCommand();
        _serializerMock.Setup(x => x.SerializeJobParam(command)).Returns(SerializedCommand);
        
        var job = GetFactory().Create(command);

        Assert.Equal(JobId,  job.Id);
        Assert.Equal(TestJobCommand.GetJobName(), job.JobName);
        Assert.Equal(SerializedCommand, job.JobParam);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Null(job.Schedule);
        Assert.Null(job.SchedulerType);
        Assert.False(job.IsExclusive);
        Assert.Equal(DateTime.UtcNow, job.CreatedAt, TimeSpan.FromSeconds(1));
        Assert.Equal(DateTime.UtcNow, job.ScheduledStartAt, TimeSpan.FromSeconds(1));
        Assert.Null(job.NextJobId);
        Assert.Equal(QueueSettings.DefaultQueueName, job.QueueName);
        Assert.True(job.CanBeRestarted);
        Assert.Null(job.SerializableGroupId);
        Assert.False(job.LockGroupIfFailed);
    }
    
    [Fact]
    public void Create_StartTimeSpecified_CreatesJobModelWithSpecifiedStartTime()
    {
        var command = new TestJobCommand();
        _serializerMock.Setup(x => x.SerializeJobParam(command)).Returns(SerializedCommand);

        var startTime = DateTime.UtcNow.AddHours(1);
        var job = GetFactory().Create(command, startTime);

        Assert.Equal(JobId,  job.Id);
        Assert.Equal(TestJobCommand.GetJobName(), job.JobName);
        Assert.Equal(SerializedCommand, job.JobParam);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Null(job.Schedule);
        Assert.Null(job.SchedulerType);
        Assert.False(job.IsExclusive);
        Assert.Equal(DateTime.UtcNow, job.CreatedAt, TimeSpan.FromSeconds(1));
        Assert.Equal(startTime, job.ScheduledStartAt);
        Assert.Null(job.NextJobId);
        Assert.Equal(QueueSettings.DefaultQueueName, job.QueueName);
        Assert.True(job.CanBeRestarted);
        Assert.Null(job.SerializableGroupId);
        Assert.False(job.LockGroupIfFailed);
    }

    [Fact]
    public void Create_OptionsSpecified_CreatesJobModelWithSpecifiedOptions()
    {
        var command = new TestJobCommand();
        _serializerMock.Setup(x => x.SerializeJobParam(command)).Returns(SerializedCommand);

        var opts = new JobOpts
        {
            CanBeRestartedIfServerGoesDown = false,
            QueueName = "queueName",
            SerializableGroupId = "groupId",
            LockGroupIfFailed = true,
            StartTime = DateTime.UtcNow.AddHours(1),
        };
        var job = GetFactory().Create(command, opts);

        Assert.Equal(JobId,  job.Id);
        Assert.Equal(TestJobCommand.GetJobName(), job.JobName);
        Assert.Equal(SerializedCommand, job.JobParam);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Null(job.Schedule);
        Assert.Null(job.SchedulerType);
        Assert.False(job.IsExclusive);
        Assert.Equal(DateTime.UtcNow, job.CreatedAt, TimeSpan.FromSeconds(1));
        Assert.Equal(opts.StartTime.Value, job.ScheduledStartAt);
        Assert.Null(job.NextJobId);
        Assert.Equal(opts.QueueName, job.QueueName);
        Assert.Equal(opts.CanBeRestartedIfServerGoesDown.Value, job.CanBeRestarted);
        Assert.Equal(opts.SerializableGroupId, job.SerializableGroupId);
        Assert.Equal(opts.LockGroupIfFailed.Value, job.LockGroupIfFailed);
    }

    [Fact]
    public void Create_CommandHasDefaultOptions_CreatesJobModelWithCommandOptions()
    {
        var defaultOpts = new JobOpts
        {
            CanBeRestartedIfServerGoesDown = false,
            QueueName = "queueName",
            SerializableGroupId = "groupId",
            LockGroupIfFailed = true,
            StartTime = DateTime.UtcNow.AddHours(1),
        };
        var command = new TestJobWithDefaultOptsCommand(defaultOpts, defaultRecurrentJobOpts: default);
        _serializerMock.Setup(x => x.SerializeJobParam(command)).Returns(SerializedCommand);

        
        var job = GetFactory().Create(command);

        Assert.Equal(JobId,  job.Id);
        Assert.Equal(TestJobWithDefaultOptsCommand.GetJobName(), job.JobName);
        Assert.Equal(SerializedCommand, job.JobParam);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Null(job.Schedule);
        Assert.Null(job.SchedulerType);
        Assert.False(job.IsExclusive);
        Assert.Equal(DateTime.UtcNow, job.CreatedAt, TimeSpan.FromSeconds(1));
        Assert.Equal(defaultOpts.StartTime.Value, job.ScheduledStartAt);
        Assert.Null(job.NextJobId);
        Assert.Equal(defaultOpts.QueueName, job.QueueName);
        Assert.Equal(defaultOpts.CanBeRestartedIfServerGoesDown.Value, job.CanBeRestarted);
        Assert.Equal(defaultOpts.SerializableGroupId, job.SerializableGroupId);
        Assert.Equal(defaultOpts.LockGroupIfFailed.Value, job.LockGroupIfFailed);
    }

    [Fact]
    public void Create_OptionsSpecifiedAndCommandHasDefault_CreatesJobModelWithMergedOptions()
    {
        var defaultOpts = new JobOpts
        {
            CanBeRestartedIfServerGoesDown = false,
            QueueName = "queueName",
            SerializableGroupId = "groupId",
            LockGroupIfFailed = true,
        };
        var command = new TestJobWithDefaultOptsCommand(defaultOpts, defaultRecurrentJobOpts: default);
        _serializerMock.Setup(x => x.SerializeJobParam(command)).Returns(SerializedCommand);

        var opts = new JobOpts
        {
            StartTime = DateTime.UtcNow.AddHours(1),
        };
        var job = GetFactory().Create(command, opts);

        Assert.Equal(JobId,  job.Id);
        Assert.Equal(TestJobWithDefaultOptsCommand.GetJobName(), job.JobName);
        Assert.Equal(SerializedCommand, job.JobParam);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Null(job.Schedule);
        Assert.Null(job.SchedulerType);
        Assert.False(job.IsExclusive);
        Assert.Equal(DateTime.UtcNow, job.CreatedAt, TimeSpan.FromSeconds(1));
        Assert.Equal(opts.StartTime.Value, job.ScheduledStartAt);
        Assert.Null(job.NextJobId);
        Assert.Equal(defaultOpts.QueueName, job.QueueName);
        Assert.Equal(defaultOpts.CanBeRestartedIfServerGoesDown.Value, job.CanBeRestarted);
        Assert.Equal(defaultOpts.SerializableGroupId, job.SerializableGroupId);
        Assert.Equal(defaultOpts.LockGroupIfFailed.Value, job.LockGroupIfFailed);
    }
    
    [Fact]
    public void CreateRecurrent_OptionsNotSpecified_CreatesJobModelWithDefaultOptions()
    {
        var command = new TestJobCommand();
        _serializerMock.Setup(x => x.SerializeJobParam(command)).Returns(SerializedCommand);
        var cron = "0 3 1 12 *";

        var job = GetFactory().CreateRecurrent(command, cron);

        Assert.Equal(JobId,  job.Id);
        Assert.Equal(TestJobCommand.GetJobName(), job.JobName);
        Assert.Equal(SerializedCommand, job.JobParam);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Equal(cron, job.Schedule);
        Assert.Equal(JobbySchedulerTypes.CronFromNow, job.SchedulerType);
        Assert.True(job.IsExclusive);
        Assert.Equal(DateTime.UtcNow, job.CreatedAt, TimeSpan.FromSeconds(1));
        var expectedStartTime = CronHelper.GetNext(cron, job.CreatedAt);
        Assert.Equal(expectedStartTime, job.ScheduledStartAt);
        Assert.Null(job.NextJobId);
        Assert.Equal(QueueSettings.DefaultQueueName, job.QueueName);
        Assert.True(job.CanBeRestarted);
    }
    
    [Fact]
    public void CreateRecurrent_CustomScheduler_CreatesJobModelWithStartTimeCalculatedByCustomScheduler()
    {
        var command = new TestJobCommand();
        _serializerMock.Setup(x => x.SerializeJobParam(command)).Returns(SerializedCommand);
        var schedule = "custom-schedule";
        var schedulerType = "custom-scheduler";
        var schedulerMock = new Mock<IScheduler>();
        var expectedStartTime = DateTime.UtcNow.AddMinutes(123);
        schedulerMock
            .Setup(x => x.GetNextStartTime(schedule, null))
            .Returns(expectedStartTime);
        var schedulers = new Dictionary<string, IScheduler>
        {
            [schedulerType] = schedulerMock.Object
        };

        var job = GetFactory(schedulers: schedulers).CreateRecurrent(command, schedule, schedulerType);

        Assert.Equal(JobId,  job.Id);
        Assert.Equal(TestJobCommand.GetJobName(), job.JobName);
        Assert.Equal(SerializedCommand, job.JobParam);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Equal(schedule, job.Schedule);
        Assert.Equal(schedulerType, job.SchedulerType);
        Assert.True(job.IsExclusive);
        Assert.Equal(DateTime.UtcNow, job.CreatedAt, TimeSpan.FromSeconds(1));
        Assert.Equal(expectedStartTime, job.ScheduledStartAt);
        Assert.Null(job.NextJobId);
        Assert.Equal(QueueSettings.DefaultQueueName, job.QueueName);
        Assert.True(job.CanBeRestarted);
    }

    [Fact]
    public void CreateRecurrent_OptionsSpecified_CreatesJobModelWithSpecifiedOptions()
    {
        var command = new TestJobCommand();
        _serializerMock.Setup(x => x.SerializeJobParam(command)).Returns(SerializedCommand);
        var cron = "0 3 1 12 *";

        var opts = new RecurrentJobOpts()
        {
            CanBeRestartedIfServerGoesDown = false,
            QueueName = "queueName",
            SerializableGroupId = "groupId",
            StartTime = DateTime.UtcNow.AddHours(1),
            IsExclusive = false
        };
        var job = GetFactory().CreateRecurrent(command, cron, opts);

        Assert.Equal(JobId,  job.Id);
        Assert.Equal(TestJobCommand.GetJobName(), job.JobName);
        Assert.Equal(SerializedCommand, job.JobParam);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Equal(cron, job.Schedule);
        Assert.Equal(JobbySchedulerTypes.CronFromNow, job.SchedulerType);
        Assert.False(job.IsExclusive);
        Assert.Equal(DateTime.UtcNow, job.CreatedAt, TimeSpan.FromSeconds(1));
        Assert.Equal(opts.StartTime.Value, job.ScheduledStartAt);
        Assert.Null(job.NextJobId);
        Assert.Equal(opts.QueueName, job.QueueName);
        Assert.Equal(opts.CanBeRestartedIfServerGoesDown.Value, job.CanBeRestarted);
        Assert.Equal(opts.SerializableGroupId, job.SerializableGroupId);
        Assert.False(job.LockGroupIfFailed);
    }

    [Fact]
    public void CreateRecurrent_CommandHasDefaultOptions_CreatesJobModelWithCommandOptions()
    {
        var defaultOpts = new RecurrentJobOpts()
        {
            CanBeRestartedIfServerGoesDown = false,
            QueueName = "queueName",
            SerializableGroupId = "groupId",
            StartTime = DateTime.UtcNow.AddHours(1),
        };
        var command = new TestJobWithDefaultOptsCommand(default, defaultOpts);
        _serializerMock.Setup(x => x.SerializeJobParam(command)).Returns(SerializedCommand);
        var cron = "0 3 1 12 *";
        
        var job = GetFactory().CreateRecurrent(command, cron);

        Assert.Equal(JobId,  job.Id);
        Assert.Equal(TestJobWithDefaultOptsCommand.GetJobName(), job.JobName);
        Assert.Equal(SerializedCommand, job.JobParam);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Equal(cron, job.Schedule);
        Assert.Equal(JobbySchedulerTypes.CronFromNow, job.SchedulerType);
        Assert.True(job.IsExclusive);
        Assert.Equal(DateTime.UtcNow, job.CreatedAt, TimeSpan.FromSeconds(1));
        Assert.Equal(defaultOpts.StartTime.Value, job.ScheduledStartAt);
        Assert.Null(job.NextJobId);
        Assert.Equal(defaultOpts.QueueName, job.QueueName);
        Assert.Equal(defaultOpts.CanBeRestartedIfServerGoesDown.Value, job.CanBeRestarted);
        Assert.Equal(defaultOpts.SerializableGroupId, job.SerializableGroupId);
        Assert.False(job.LockGroupIfFailed);
    }

    [Fact]
    public void CreateRecurrent_OptionsSpecifiedAndCommandHasDefault_CreatesJobModelWithMergedOptions()
    {
        var defaultOpts = new RecurrentJobOpts()
        {
            CanBeRestartedIfServerGoesDown = false,
            QueueName = "queueName",
            SerializableGroupId = "groupId",
            IsExclusive = false,
        };
        var command = new TestJobWithDefaultOptsCommand(default, defaultOpts);
        _serializerMock.Setup(x => x.SerializeJobParam(command)).Returns(SerializedCommand);
        var cron = "0 3 1 12 *";

        var opts = new RecurrentJobOpts
        {
            StartTime = DateTime.UtcNow.AddHours(1),
        };
        var job = GetFactory().CreateRecurrent(command, cron, opts);

        Assert.Equal(JobId,  job.Id);
        Assert.Equal(TestJobWithDefaultOptsCommand.GetJobName(), job.JobName);
        Assert.Equal(SerializedCommand, job.JobParam);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Equal(cron, job.Schedule);
        Assert.Equal(JobbySchedulerTypes.CronFromNow, job.SchedulerType);
        Assert.False(job.IsExclusive);
        Assert.Equal(DateTime.UtcNow, job.CreatedAt, TimeSpan.FromSeconds(1));
        Assert.Equal(opts.StartTime.Value, job.ScheduledStartAt);
        Assert.Null(job.NextJobId);
        Assert.Equal(defaultOpts.QueueName, job.QueueName);
        Assert.Equal(defaultOpts.CanBeRestartedIfServerGoesDown.Value, job.CanBeRestarted);
        Assert.Equal(defaultOpts.SerializableGroupId, job.SerializableGroupId);
        Assert.False(job.LockGroupIfFailed);
    }
    
    [Fact]
    public void CreateRecurrent_DefaultRecurrentQueueSpecified_CreatesJobModelWithDefaultRecurrentQueue()
    {
        var command = new TestJobCommand();
        _serializerMock.Setup(x => x.SerializeJobParam(command)).Returns(SerializedCommand);
        var cron = "0 3 1 12 *";
        var defaultRecurrentQueue = "defaultRecurrentQueue";

        var job = GetFactory(defaultRecurrentQueue).CreateRecurrent(command, cron);

        Assert.Equal(JobId,  job.Id);
        Assert.Equal(TestJobCommand.GetJobName(), job.JobName);
        Assert.Equal(SerializedCommand, job.JobParam);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Equal(cron, job.Schedule);
        Assert.Equal(JobbySchedulerTypes.CronFromNow, job.SchedulerType);
        Assert.True(job.IsExclusive);
        Assert.Equal(DateTime.UtcNow, job.CreatedAt, TimeSpan.FromSeconds(1));
        var expectedStartTime = CronHelper.GetNext(cron, job.CreatedAt);
        Assert.Equal(expectedStartTime, job.ScheduledStartAt);
        Assert.Null(job.NextJobId);
        Assert.Equal(defaultRecurrentQueue, job.QueueName);
        Assert.True(job.CanBeRestarted);
    }
    
    private JobsFactory GetFactory(string? defaultRecurrentQueue = null, Dictionary<string, IScheduler>? schedulers = null)
    {
        return new JobsFactory(_guidGeneratorMock.Object,
            _serializerMock.Object,
            schedulers ?? JobbySchedulerTypes.CreateSchedulers(),
            defaultRecurrentQueue);
    }
}
