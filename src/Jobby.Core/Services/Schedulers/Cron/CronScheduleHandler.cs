using Jobby.Core.Helpers;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models.Schedulers;

namespace Jobby.Core.Services.Schedulers.Cron;

internal class CronScheduleHandler : IScheduleHandler<CronSchedule>
{
    public DateTime GetFirstStartTime(CronSchedule schedule, DateTime utcNow) => schedule.CronExpression.GetNext(utcNow);

    public DateTime GetNextStartTime(CronSchedule schedule, in SchedulerExecutionContext ctx)
    {
        var from = schedule.CalculateNextFromPrev
            ? ctx.PreviousScheduledStartTime
            : ctx.UtcNow;

        return schedule.CronExpression.GetNext(from);
    }
}