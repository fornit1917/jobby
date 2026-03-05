using Jobby.Core.Models.Schedulers;

namespace Jobby.Core.Interfaces.Schedulers;

internal interface IScheduleExecutor
{
    bool TryGetNextStartTime(string schedule, in SchedulerExecutionContext ctx, out DateTime nextStartTime);
}

public interface IScheduleHandler<TSchedule> where TSchedule : ISchedule
{
    DateTime GetFirstStartTime(DateTime utcNow);
    DateTime GetNextStartTime(TSchedule schedule, in SchedulerExecutionContext ctx);
}