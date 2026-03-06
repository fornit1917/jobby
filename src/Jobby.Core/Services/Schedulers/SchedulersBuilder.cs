using System.Collections.Frozen;

using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Core.Services.Schedulers;
internal class SchedulersBuilder
{
    private readonly Dictionary<string, IScheduleExecutor> _schedulersByTypes = new();

    public SchedulersBuilder AddScheduler<TSchedule, TScheduleHandler, TScheduleSerializer>()
        where TSchedule : ISchedule
        where TScheduleHandler : IScheduleHandler<TSchedule>, new()
        where TScheduleSerializer : IJobParamSerializer<TSchedule>, new()
    {
        return AddScheduler<TSchedule, TScheduleHandler>(new TScheduleSerializer());
    }

    public SchedulersBuilder AddScheduler<TSchedule, TScheduleHandler>(IJobParamSerializer<TSchedule>? scheduleSerializer = null)
        where TSchedule : ISchedule
        where TScheduleHandler : IScheduleHandler<TSchedule>, new()
    {
        return AddScheduler<TSchedule>(new TScheduleHandler());
    }

    public SchedulersBuilder AddScheduler<TSchedule>(
        IScheduleHandler<TSchedule> sheduleHandler,
        IJobParamSerializer<TSchedule>? scheduleSerializer = null
    )
        where TSchedule : ISchedule
    {
        var schedulerType = TSchedule.GetSchedulerType();
        if (!_schedulersByTypes.TryAdd(
            schedulerType,
            new ScheduleExecutor<TSchedule>(sheduleHandler, scheduleSerializer)
        ))
        {
            throw new InvalidJobsConfigException($"Handler for schedule {typeof(TSchedule)} has already been added");
        }

        return this;
    }

    private FrozenDictionary<string, IScheduleExecutor>? _result;
    public FrozenDictionary<string, IScheduleExecutor> Build()
    {
        if (_result is null)
            _result = _schedulersByTypes.ToFrozenDictionary();

        return _result;
    }
}
