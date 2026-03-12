using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Core.Services.Schedulers;

internal class CronScheduleHandler : BaseScheduleHandler<CronSchedule>
{
    public const string Name = "JOBBY_CRON";
    
    public override string GetSchedulerTypeName() => Name;

    public override string SerializeSchedule(CronSchedule schedule)
    {
        if (!schedule.CalculateNextFromPrev)
            return schedule.CronExpression;

        return base.SerializeSchedule(schedule);
    }

    public override CronSchedule DeserializeSchedule(string schedule)
    {
        // For backward compatibility with old cron without parameters
        if (!schedule.StartsWith('{'))
        {
            return new CronSchedule
            {
                CronExpression = schedule,
                CalculateNextFromPrev = false
            };            
        }
        
        return base.DeserializeSchedule(schedule);
    }

    public override DateTime GetFirstStartTime(CronSchedule schedule, DateTime utcNow)
    {
        return CronHelper.GetNext(schedule.CronExpression, utcNow);
    }

    public override DateTime GetNextStartTime(CronSchedule schedule, ScheduleCalculationContext ctx)
    {
        return schedule.CalculateNextFromPrev
            ? CronHelper.GetNext(schedule.CronExpression, ctx.PrevScheduledTime)
            : CronHelper.GetNext(schedule.CronExpression, ctx.UtcNow);
    }
}