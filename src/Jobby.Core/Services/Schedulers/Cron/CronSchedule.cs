using Cronos;
using Jobby.Core.Helpers;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models.Schedulers;

namespace Jobby.Core.Services.Schedulers.Cron;

internal class CronSchedule : IScheduler
{
    public readonly CronExpression CronExpression;
    public readonly bool CalculateNextFromPrev;

    public CronSchedule(CronExpression cronExpression, bool calculateNextFromPrev)
    {
        CronExpression = cronExpression ?? throw new ArgumentNullException(nameof(cronExpression));
        CalculateNextFromPrev = calculateNextFromPrev;
    }

    public DateTime GetFirstStartTime(DateTime utcNow) => CronExpression.GetNext(utcNow);

    public DateTime GetNextStartTime(in SchedulerExecutionContext ctx)
    {
        var from = CalculateNextFromPrev
            ? ctx.PreviousScheduledStartTime
            : ctx.UtcNow;

        return CronExpression.GetNext(from);
    }
}
