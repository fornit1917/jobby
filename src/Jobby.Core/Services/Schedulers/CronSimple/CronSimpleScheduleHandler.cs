using Jobby.Core.Helpers;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models.Schedulers;

namespace Jobby.Core.Services.Schedulers.CronSimple;

internal class CronSimpleScheduleHandler : IScheduleHandler<CronSimpleSchedule>
{
    public DateTime GetFirstStartTime(CronSimpleSchedule schedule, DateTime utcNow) => schedule.CronExpression.GetNext(utcNow);
    public DateTime GetNextStartTime(CronSimpleSchedule schedule, in SchedulerExecutionContext ctx) => schedule.CronExpression.GetNext(ctx.UtcNow);
}