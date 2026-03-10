using Cronos;
using Jobby.Core.Helpers;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models.Schedulers;

namespace Jobby.Core.Services.Schedulers.CronSimple;

internal class CronSimpleScheduler : IScheduler
{
    public readonly CronExpression CronExpression;

    public CronSimpleScheduler(CronExpression cronExpression)
    {
        CronExpression = cronExpression ?? throw new ArgumentNullException(nameof(cronExpression));
    }

    public DateTime GetFirstStartTime(DateTime utcNow) => CronExpression.GetNext(utcNow);
    public DateTime GetNextStartTime(in SchedulerExecutionContext ctx) => CronExpression.GetNext(ctx.UtcNow);
}
