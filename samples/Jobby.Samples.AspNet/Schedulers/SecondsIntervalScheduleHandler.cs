using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Services.Schedulers;

namespace Jobby.Samples.AspNet.Schedulers;

public class SecondsIntervalScheduleHandler : BaseScheduleHandler<SecondsIntervalSchedule>
{
    public override string GetSchedulerTypeName() => "CUSTOM_SECONDS_INTERVAL";

    public override DateTime GetFirstStartTime(SecondsIntervalSchedule schedule, DateTime utcNow)
    {
        return utcNow.AddSeconds(schedule.SecondsInterval);
    }

    public override DateTime GetNextStartTime(SecondsIntervalSchedule schedule, ScheduleCalculationContext ctx)
    {
        return ctx.UtcNow.AddSeconds(schedule.SecondsInterval);
    }
}