using Jobby.Core.Exceptions;
using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models;
using Jobby.Core.Services.Schedulers;

namespace Jobby.Core.Services;

internal class JobsFactory : IJobsFactory
{
    private readonly IGuidGenerator _guidGenerator;
    private readonly IJobParamSerializer _serializer;
    private readonly ISchedulersRegistry _schedulersRegistry;
    private readonly ITimerService _timerService;
    private readonly string _defaultQueueForRecurrent;

    public JobsFactory(IGuidGenerator guidGenerator,
        IJobParamSerializer serializer,
        ISchedulersRegistry schedulersRegistry,
        ITimerService timerService,
        string? defaultQueueForRecurrent)
    {
        _guidGenerator = guidGenerator;
        _serializer = serializer;
        _schedulersRegistry = schedulersRegistry;
        _timerService = timerService;
        _defaultQueueForRecurrent = defaultQueueForRecurrent ?? QueueSettings.DefaultQueueName;
    }

    public JobCreationModel Create<TCommand>(TCommand command, JobOpts opts = default)
        where TCommand : IJobCommand
    {
        var jobName = TCommand.GetJobName();
        var defaultOpts = default(JobOpts);
        if (command is IHasDefaultJobOptions hasDefaultOpts)
        {
            defaultOpts = hasDefaultOpts.GetOptionsForEnqueuedJob();
        }
        
        return new JobCreationModel
        {
            Id = _guidGenerator.NewGuid(),
            CreatedAt = _timerService.GetUtcNow(),
            JobName = jobName,
            JobParam = _serializer.SerializeJobParam(command),
            ScheduledStartAt = opts.StartTime 
                               ?? defaultOpts.StartTime 
                               ?? _timerService.GetUtcNow(),
            Status = JobStatus.Scheduled,
            CanBeRestarted = opts.CanBeRestartedIfServerGoesDown 
                             ?? defaultOpts.CanBeRestartedIfServerGoesDown 
                             ?? true,
            QueueName = opts.QueueName 
                        ?? defaultOpts.QueueName 
                        ?? QueueSettings.DefaultQueueName,
            SerializableGroupId = opts.SerializableGroupId ?? defaultOpts.SerializableGroupId,
            LockGroupIfFailed = opts.LockGroupIfFailed 
                                ?? defaultOpts.LockGroupIfFailed 
                                ?? false
        };    
    }

    public JobCreationModel Create<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand
    {
        return Create(command, new JobOpts { StartTime = startTime });
    }

    public JobCreationModel CreateRecurrent<TCommand>(TCommand command,
        string cron,
        RecurrentJobOpts opts = default) where TCommand : IJobCommand
    {
        var cronSchedule = new CronSchedule
        {
            CronExpression = cron
        };
        return CreateRecurrent(command, cronSchedule, opts);
    }

    public JobCreationModel CreateRecurrent<TCommand, TSchedule>(TCommand command, 
        TSchedule schedule,
        RecurrentJobOpts opts = default) 
            where TCommand : IJobCommand
            where TSchedule : ISchedule
    {
        var jobName = TCommand.GetJobName();
        var defaultOpts = default(RecurrentJobOpts);
        if (command is IHasDefaultJobOptions hasDefaultOpts)
        {
            defaultOpts = hasDefaultOpts.GetOptionsForRecurrentJob();
        }
        
        var scheduler = _schedulersRegistry.GetScheduler<TSchedule>();
        if (scheduler == null)
        {
            throw new InvalidScheduleException($"No scheduler registered for {typeof(TSchedule)}");
        }
        
        return new JobCreationModel
        {
            Id = _guidGenerator.NewGuid(),
            JobParam = _serializer.SerializeJobParam(command),
            JobName = jobName,
            Schedule = scheduler.SerializeSchedule(schedule),
            SchedulerType = scheduler.GetSchedulerTypeName(),
            IsExclusive = opts.IsExclusive ?? defaultOpts.IsExclusive ?? true,
            CreatedAt = _timerService.GetUtcNow(),
            Status = JobStatus.Scheduled,
            ScheduledStartAt = opts.StartTime 
                               ?? defaultOpts.StartTime
                               ?? scheduler.GetFirstStartTime(schedule, _timerService.GetUtcNow()),
            CanBeRestarted = opts.CanBeRestartedIfServerGoesDown
                             ??  defaultOpts.CanBeRestartedIfServerGoesDown
                             ?? true,
            QueueName = opts.QueueName 
                        ?? defaultOpts.QueueName
                        ?? _defaultQueueForRecurrent,
            SerializableGroupId = opts.SerializableGroupId ?? defaultOpts.SerializableGroupId,
        };
    }

    public JobsSequenceBuilder CreateSequenceBuilder()
    {
        return new JobsSequenceBuilder(this);
    }

    public JobsSequenceBuilder CreateSequenceBuilder(int capacity)
    {
        return new JobsSequenceBuilder(capacity, this);
    }
}
