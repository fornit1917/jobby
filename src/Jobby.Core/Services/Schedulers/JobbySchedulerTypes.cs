using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Core.Services.Schedulers;

public static class JobbySchedulerTypes
{
    public const string CronFromNow = "CRON_FROM_NOW";
    public const string CronFromPrev = "CRON_FROM_PREV";
    public const string TimeSpanFromNow = "TIMESPAN_FROM_NOW";
    public const string TimeSpanFromPrev = "TIMESPAN_FROM_PREV";

    public static Dictionary<string, ISchedule> CreateSchedulers()
    {
        return new()
        {
            [CronFromNow] = new CronScheduler(TimerService.Instance, calculateNextFromPrev: false),
            [CronFromPrev] = new CronScheduler(TimerService.Instance, calculateNextFromPrev: true),
            [TimeSpanFromNow] = new TimeSpanScheduler(TimerService.Instance, calculateNextFromPrev: false),
            [TimeSpanFromPrev] = new TimeSpanScheduler(TimerService.Instance, calculateNextFromPrev: true)
        };
    }
}