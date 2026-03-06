using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models.Schedulers;

namespace Jobby.Samples.AspNet.Schedulers;

public class CustomSecondsScheduler : ISchedule
{
    public static string GetSchedulerType() => "CUSTOM_SECONDS";
    public int Seconds { get; init; }
}

public class CustomSecondsSchedulerHandler : IScheduleHandler<CustomSecondsScheduler>
{
    public DateTime GetFirstStartTime(CustomSecondsScheduler schedule, DateTime utcNow) => utcNow;

    public DateTime GetNextStartTime(string schedule, DateTime? previousScheduledStartTime)
    {
        var seconds = int.Parse(schedule);
        return previousScheduledStartTime?.AddSeconds(seconds) ?? DateTime.UtcNow;
    }

    public DateTime GetNextStartTime(CustomSecondsScheduler schedule, in SchedulerExecutionContext ctx)
    {
        return ctx.PreviousScheduledStartTime.AddSeconds(schedule.Seconds > 0 ? schedule.Seconds : 1);
    }
}