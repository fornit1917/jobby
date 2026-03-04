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
    private readonly IReadOnlyDictionary<string, IScheduler> _schedulersByType;
    private readonly string _defaultQueueForRecurrent;

    public JobsFactory(IGuidGenerator guidGenerator,
        IJobParamSerializer serializer,
        IReadOnlyDictionary<string, IScheduler> schedulersByType,
        string? defaultQueueForRecurrent)
    {
        _guidGenerator = guidGenerator;
        _serializer = serializer;
        _schedulersByType = schedulersByType;
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
            CreatedAt = DateTime.UtcNow,
            JobName = jobName,
            JobParam = _serializer.SerializeJobParam(command),
            ScheduledStartAt = opts.StartTime 
                               ?? defaultOpts.StartTime 
                               ?? DateTime.UtcNow,
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
        return CreateRecurrent(command, cron, JobbySchedulerTypes.CronFromNow, opts);
    }

    public JobCreationModel CreateRecurrent<TCommand>(TCommand command, 
        string schedule,
        string schedulerType,
        RecurrentJobOpts opts = default) where TCommand : IJobCommand
    {
        if (!_schedulersByType.TryGetValue(schedulerType, out var scheduler))
        {
            throw new UnknownSchedulerTypeException($"Unknown scheduler type: {schedulerType}");
        }
        
        var jobName = TCommand.GetJobName();
        var defaultOpts = default(RecurrentJobOpts);
        if (command is IHasDefaultJobOptions hasDefaultOpts)
        {
            defaultOpts = hasDefaultOpts.GetOptionsForRecurrentJob();
        }
        
        return new JobCreationModel
        {
            Id = _guidGenerator.NewGuid(),
            JobParam = _serializer.SerializeJobParam(command),
            JobName = jobName,
            Schedule = schedule,
            SchedulerType = schedulerType,
            IsExclusive = opts.IsExclusive ?? defaultOpts.IsExclusive ?? true,
            CreatedAt = DateTime.UtcNow,
            Status = JobStatus.Scheduled,
            ScheduledStartAt = opts.StartTime 
                               ?? defaultOpts.StartTime
                               ?? scheduler.GetNextStartTime(schedule, previousScheduledStartTime: null),
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
