using Cronos;
using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Core.Services.Schedulers.Cron;

internal class CronSchedule : ISchedule
{
    public readonly CronExpression CronExpression;
    public readonly bool CalculateNextFromPrev;

    public CronSchedule(CronExpression cronExpression, bool calculateNextFromPrev)
    {
        CronExpression = cronExpression ?? throw new ArgumentNullException(nameof(cronExpression));
        CalculateNextFromPrev = calculateNextFromPrev;
    }

    static string ISchedule.GetSchedulerType() => "__JOBBY_CRON";
}
