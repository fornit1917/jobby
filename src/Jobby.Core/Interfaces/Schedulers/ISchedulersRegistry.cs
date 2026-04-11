namespace Jobby.Core.Interfaces.Schedulers;

internal interface ISchedulersRegistry
{
    IScheduleHandler<TSchedule>? GetScheduler<TSchedule>() where TSchedule : ISchedule;
    SchedulerFunction? GetSchedulerAsFunction(string schedulerType);
}