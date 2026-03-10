using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models.Schedulers;

namespace Jobby.Core.Services.Schedulers;
internal class ScheduleExecutor<TSchedule> : ISchedulerExecutor
    where TSchedule : IScheduler
{
    private readonly IScheduleSerializer<TSchedule> _scheduleSerializer;

    public ScheduleExecutor(IScheduleSerializer<TSchedule> scheduleSerializer)
    {
        _scheduleSerializer = scheduleSerializer;
    }

    bool ISchedulerExecutor.TryGetNextStartTime(string schedule, in SchedulerExecutionContext ctx, out DateTime nextStartTime)
    {
        if (!_scheduleSerializer.TryDeserialize(schedule, out var scheduleOptions))
        {
            nextStartTime = default;
            return false;
        }

        nextStartTime = scheduleOptions.GetNextStartTime(ctx);
        return true;
    }
}
