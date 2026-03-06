using Cronos;

using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Core.Services.Schedulers.CronSimple;

public static class DefaultScheduler
{
    public const string SCHEDULER_TYPE = "__JOBBY_CRON_SIMPLE";
}

internal class CronSimpleSchedule : ISchedule
{
    public readonly CronExpression CronExpression;

    public CronSimpleSchedule(CronExpression cronExpression)
    {
        CronExpression = cronExpression ?? throw new ArgumentNullException(nameof(cronExpression));
    }

    static string ISchedule.GetSchedulerType() => DefaultScheduler.SCHEDULER_TYPE;
}
