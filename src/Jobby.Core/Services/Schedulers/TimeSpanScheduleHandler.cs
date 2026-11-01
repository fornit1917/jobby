using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Core.Services.Schedulers;

internal class TimeSpanScheduleHandler : BaseScheduleHandler<TimeSpanSchedule>
{
    public override string GetSchedulerTypeName() => "JOBBY_INTERVAL";

    public override DateTime GetFirstStartTime(TimeSpanSchedule schedule, DateTime utcNow)
    {
        return utcNow;
    }

    public override DateTime GetNextStartTime(TimeSpanSchedule schedule, ScheduleCalculationContext ctx)
    {
        EnsureScheduleIsValid(schedule);
        return schedule.CalculateNextFromPrev
            ? ctx.PrevScheduledTime.Add(schedule.Interval)
            : ctx.UtcNow.Add(schedule.Interval);
    }

    public override void EnsureScheduleIsValid(TimeSpanSchedule schedule)
    {
        if (schedule.Interval <= TimeSpan.Zero)
        {
            throw new InvalidScheduleException(
                $"Interval for TimeSpanSchedule must be positive, but '{schedule.Interval}'");
        }
    }
}