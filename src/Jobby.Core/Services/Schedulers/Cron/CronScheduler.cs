using Cronos;

using Jobby.Core.Helpers;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models.Schedulers;

namespace Jobby.Core.Services.Schedulers.Cron;

internal class CronScheduler : IScheduler
{
    public readonly CronExpression CronExpression;
    public readonly bool CalculateNextFromPrev;

    public CronScheduler(string cronExpression, bool calculateNextFromPrev)
    {
        CronExpression = CronHelper.Parse(cronExpression ?? throw new ArgumentNullException(nameof(cronExpression)));
        CalculateNextFromPrev = calculateNextFromPrev;
    }

    public CronScheduler(CronExpression cronExpression, bool calculateNextFromPrev)
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
