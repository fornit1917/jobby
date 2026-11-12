using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models;
using Jobby.Core.Services.Schedulers;
using System.Diagnostics;

namespace Jobby.Core.Services;

internal class JobsFactory : IJobsFactory
{
    private readonly IGuidGenerator _guidGenerator;
    private readonly IJobParamSerializer _serializer;
    private readonly ISchedulersRegistry _schedulersRegistry;
    private readonly ITimerService _timerService;
    private readonly string _defaultQueueForRecurrent;
    private readonly JobbyTracingSettings? _tracingSettings;

    public JobsFactory(IGuidGenerator guidGenerator,
        IJobParamSerializer serializer,
        ISchedulersRegistry schedulersRegistry,
        ITimerService timerService,
        string? defaultQueueForRecurrent,
        JobbyTracingSettings? tracingSettings = null)
    {
        _guidGenerator = guidGenerator;
        _serializer = serializer;
        _schedulersRegistry = schedulersRegistry;
        _timerService = timerService;
        _defaultQueueForRecurrent = defaultQueueForRecurrent ?? QueueSettings.DefaultQueueName;
        _tracingSettings = tracingSettings;
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

        var (tracingMode, traceparent) = CaptureTracingContext(opts.TracingMode ?? defaultOpts.TracingMode);

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
                                ?? false,
            TracingMode = tracingMode,
            Traceparent = traceparent
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

    /// <summary>
    /// Resolves the effective <see cref="JobbyTracingMode"/> (per-job opts fall back to the global
    /// UseTracing setting) and, for RestoreTraceId/LinkTraceId modes, captures the W3C traceparent
    /// ("00-{trace-id}-{span-id}-{flags}") of the current ambient Activity.
    /// Returns null traceparent when the mode is CreateNewTraceId, there is no ambient Activity,
    /// or the ambient Activity uses a non-W3C ID format (its TraceId/SpanId are then all zeros
    /// and cannot form a valid traceparent; execution falls back to starting a new trace).
    /// </summary>
    private (JobbyTracingMode Mode, string? Traceparent) CaptureTracingContext(JobbyTracingMode? optsTracingMode)
    {
        var mode = optsTracingMode ?? _tracingSettings?.TracingMode ?? JobbyTracingMode.CreateNewTraceId;
        if (mode == JobbyTracingMode.CreateNewTraceId)
        {
            return (mode, null);
        }

        var current = Activity.Current;
        if (current == null || current.IdFormat != ActivityIdFormat.W3C)
        {
            return (mode, null);
        }

        var traceparent = $"00-{current.TraceId.ToHexString()}-{current.SpanId.ToHexString()}-{((int)current.ActivityTraceFlags):x2}";
        return (mode, traceparent);
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

        // Validate even when the first start time is overridden by opts.StartTime,
        // so an invalid schedule fails the creation instead of falling back to another schedule at runtime
        scheduler.EnsureScheduleIsValid(schedule);

        // Traceparent is intentionally left null and the mode pinned to CreateNewTraceId:
        // recurrent jobs always start a new trace on every run
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
            TracingMode = JobbyTracingMode.CreateNewTraceId,
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
