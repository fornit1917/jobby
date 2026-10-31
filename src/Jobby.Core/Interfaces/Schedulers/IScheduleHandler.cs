namespace Jobby.Core.Interfaces.Schedulers;

public interface IScheduleHandler<TSchedule> where TSchedule : ISchedule
{
    string GetSchedulerTypeName();
    
    string SerializeSchedule(TSchedule schedule);
    TSchedule DeserializeSchedule(string schedule);
    
    DateTime GetFirstStartTime(TSchedule schedule, DateTime utcNow);
    DateTime GetNextStartTime(TSchedule schedule, ScheduleCalculationContext ctx);

    /// <summary>
    /// Ensures the schedule is valid, otherwise throws <see cref="Exceptions.InvalidScheduleException"/>.
    /// Called when a job is being created, so an invalid schedule fails the creation instead of silently
    /// falling back to another schedule at runtime.
    /// </summary>
    void EnsureScheduleIsValid(TSchedule schedule)
    {
    }
}