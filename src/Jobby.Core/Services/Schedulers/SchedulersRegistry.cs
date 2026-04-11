using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Core.Services.Schedulers;

internal class SchedulersRegistry : ISchedulersRegistry
{
    private readonly IReadOnlyDictionary<Type, object> _schedulersByScheduleClassType;
    private readonly IReadOnlyDictionary<string, SchedulerFunction> _schedulersFunctionsByTypeName;

    public SchedulersRegistry(IReadOnlyDictionary<Type, object> schedulersByScheduleClassType, 
        IReadOnlyDictionary<string, SchedulerFunction> schedulersFunctionsByTypeName)
    {
        _schedulersByScheduleClassType = schedulersByScheduleClassType;
        _schedulersFunctionsByTypeName = schedulersFunctionsByTypeName;
    }

    public IScheduleHandler<TSchedule>? GetScheduler<TSchedule>() where TSchedule : ISchedule
    {
        return _schedulersByScheduleClassType.GetValueOrDefault(typeof(TSchedule)) as IScheduleHandler<TSchedule>;
    }

    public SchedulerFunction? GetSchedulerAsFunction(string schedulerType)
    {
        return _schedulersFunctionsByTypeName.GetValueOrDefault(schedulerType);
    }
}