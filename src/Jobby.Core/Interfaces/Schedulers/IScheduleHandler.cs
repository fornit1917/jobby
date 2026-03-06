using Jobby.Core.Models.Schedulers;

namespace Jobby.Core.Interfaces.Schedulers;

public interface IScheduleHandler<TSchedule> where TSchedule : ISchedule
{
    DateTime GetFirstStartTime(TSchedule schedule, DateTime utcNow);
    DateTime GetNextStartTime(TSchedule schedule, in SchedulerExecutionContext ctx);
}