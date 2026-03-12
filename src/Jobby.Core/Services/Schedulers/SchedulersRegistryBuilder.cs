using System.Collections.Frozen;
using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Core.Services.Schedulers;

internal class SchedulersRegistryBuilder
{
    private readonly Dictionary<Type, object> _schedulersByScheduleClassType = new();
    private readonly Dictionary<string, SchedulerFunction> _schedulersFunctionsByTypeName = new();

    public SchedulersRegistryBuilder AddScheduler<TSchedule>(IScheduleHandler<TSchedule> scheduleHandler) where TSchedule : ISchedule
    {
        if (!_schedulersByScheduleClassType.TryAdd(typeof(TSchedule), scheduleHandler))
        {
            throw new InvalidBuilderConfigException($"Scheduler for type {typeof(TSchedule).Name} has already been registered");
        }

        DateTime SchedulerFunction(string rawSchedule, ScheduleCalculationContext ctx)
        {
            try
            {
                var schedule = scheduleHandler.DeserializeSchedule(rawSchedule);
                return scheduleHandler.GetNextStartTime(schedule, ctx);
            }
            catch (Exception e)
            {
                throw new InvalidScheduleException("Could not calculate next start time", e);
            }
        }

        if (!_schedulersFunctionsByTypeName.TryAdd(scheduleHandler.GetSchedulerTypeName(), SchedulerFunction))
        {
            throw new InvalidBuilderConfigException($"Scheduler with type {scheduleHandler.GetSchedulerTypeName()} has already been registered");
        }

        return this;
    }

    public SchedulersRegistry CreateRegistry()
    {
        return new SchedulersRegistry(_schedulersByScheduleClassType.ToFrozenDictionary(),
            _schedulersFunctionsByTypeName.ToFrozenDictionary());
    }
}