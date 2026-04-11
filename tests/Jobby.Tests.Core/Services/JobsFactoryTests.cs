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
    private const string CustomSchedulerTypeName = "CustomSchedulerType";
    
    private readonly Mock<IGuidGenerator> _guidGeneratorMock;
    private readonly Mock<IJobParamSerializer> _serializerMock;
    private readonly Mock<IScheduleHandler<CustomSchedule>> _customSchedulerMock;
    private readonly Mock<ITimerService> _timerServiceMock;

    // ReSharper disable once MemberCanBePrivate.Global
    public class CustomSchedule : ISchedule
    {
        public int Value { get; init; }
    }

    public JobsFactoryTests()
    {
        _serializerMock = new Mock<IJobParamSerializer>();
        _customSchedulerMock = new Mock<IScheduleHandler<CustomSchedule>>();
        _customSchedulerMock.Setup(x => x.GetSchedulerTypeName()).Returns(CustomSchedulerTypeName);
        _timerServiceMock = new Mock<ITimerService>();
        _timerServiceMock.Setup(x => x.GetUtcNow()).Returns(() => DateTime.UtcNow);
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
        Assert.Equal("JOBBY_CRON", job.SchedulerType);
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
        
        var schedule = new CustomSchedule { Value = 123 };
        var now = DateTime.UtcNow;
        var expectedStartTime = DateTime.UtcNow.AddHours(1);
        _timerServiceMock.Setup(x => x.GetUtcNow()).Returns(now);
        _customSchedulerMock
            .Setup(x => x.GetFirstStartTime(schedule, now))
            .Returns(expectedStartTime);
        var expectedSerializedSchedule = "serializedCustomSchedule";
        _customSchedulerMock
            .Setup(x => x.SerializeSchedule(schedule))
            .Returns(expectedSerializedSchedule);

        var job = GetFactory().CreateRecurrent(command, schedule);

        Assert.Equal(JobId,  job.Id);
        Assert.Equal(TestJobCommand.GetJobName(), job.JobName);
        Assert.Equal(SerializedCommand, job.JobParam);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Equal(expectedSerializedSchedule, job.Schedule);
        Assert.Equal(CustomSchedulerTypeName, job.SchedulerType);
        Assert.True(job.IsExclusive);
        Assert.Equal(now, job.CreatedAt, TimeSpan.FromSeconds(1));
        Assert.Equal(expectedStartTime, job.ScheduledStartAt, TimeSpan.FromSeconds(1));
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
        Assert.Equal("JOBBY_CRON", job.SchedulerType);
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
        Assert.Equal("JOBBY_CRON", job.SchedulerType);
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
        Assert.Equal("JOBBY_CRON", job.SchedulerType);
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
        Assert.Equal("JOBBY_CRON", job.SchedulerType);
        Assert.True(job.IsExclusive);
        Assert.Equal(DateTime.UtcNow, job.CreatedAt, TimeSpan.FromSeconds(1));
        var expectedStartTime = CronHelper.GetNext(cron, job.CreatedAt);
        Assert.Equal(expectedStartTime, job.ScheduledStartAt);
        Assert.Null(job.NextJobId);
        Assert.Equal(defaultRecurrentQueue, job.QueueName);
        Assert.True(job.CanBeRestarted);
    }
    
    private JobsFactory GetFactory(string? defaultRecurrentQueue = null)
    {
        var schedulers = new SchedulersRegistryBuilder()
            .AddScheduler(new CronScheduleHandler())
            .AddScheduler(new TimeSpanScheduleHandler())
            .AddScheduler(_customSchedulerMock.Object)
            .CreateRegistry();
        
        return new JobsFactory(_guidGeneratorMock.Object,
            _serializerMock.Object,
            schedulers,
            _timerServiceMock.Object,
            defaultRecurrentQueue);
    }
}
