using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models;
using Jobby.Core.Services.Schedulers;

namespace Jobby.Core.Services;

internal class JobsFactory : IJobsFactory
{
    private readonly IGuidGenerator _guidGenerator;
    private readonly IJobParamSerializer _serializer;
    private readonly ISchedulerStorageOptionsProvider _schedulerStorageOptionsProvider;
    private readonly string _defaultQueueForRecurrent;

    public JobsFactory(IGuidGenerator guidGenerator,
        IJobParamSerializer serializer,
        ISchedulerStorageOptionsProvider schedulerStorageOptionsProvider,
        string? defaultQueueForRecurrent)
    {
        _guidGenerator = guidGenerator;
        _serializer = serializer;
        _schedulerStorageOptionsProvider = schedulerStorageOptionsProvider;
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


    public JobCreationModel CreateRecurrent<TCommand, TScheduler>(TCommand command,
        TScheduler scheduler,
        RecurrentJobOpts opts = default
    )
        where TCommand : IJobCommand
        where TScheduler : IScheduler
    {
        var (schedulerType, schedulerOptions) = _schedulerStorageOptionsProvider.GetStorageOptions<TScheduler>(scheduler);

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
            Schedule = schedulerOptions,
            SchedulerType = schedulerType,
            IsExclusive = opts.IsExclusive ?? defaultOpts.IsExclusive ?? true,
            CreatedAt = DateTime.UtcNow,
            Status = JobStatus.Scheduled,
            ScheduledStartAt = opts.StartTime 
                               ?? defaultOpts.StartTime
                               ?? scheduler.GetFirstStartTime(TimerService.Instance.GetUtcNow()),
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
