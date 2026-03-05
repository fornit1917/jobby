using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models.Schedulers;

namespace Jobby.Core.Services.Schedulers;
internal class ScheduleExecutor<TSchedule, TScheduleHandler, TScheduleSerializer> : IScheduleExecutor
    where TSchedule : ISchedule
    where TScheduleHandler : IScheduleHandler<TSchedule>
    where TScheduleSerializer : IScheduleSerializer<TSchedule>
{
    private readonly IScheduleSerializer<TSchedule> _scheduleSerializer;
    private readonly IScheduleHandler<TSchedule> _scheduleHandler;

    public ScheduleExecutor(IScheduleSerializer<TSchedule> scheduleSerializer, IScheduleHandler<TSchedule> scheduleHandler)
    {
        _scheduleSerializer = scheduleSerializer ?? throw new ArgumentNullException(nameof(scheduleSerializer));
        _scheduleHandler = scheduleHandler ?? throw new ArgumentNullException(nameof(scheduleHandler));
    }

    public bool TryGetNextStartTime(string schedule, in SchedulerExecutionContext ctx, out DateTime nextStartTime)
    {
        if (!_scheduleSerializer.TryDeserialize(schedule, out var scheduleOptions))
        {
            nextStartTime = default;
            return false;
        }

        nextStartTime = _scheduleHandler.GetNextStartTime(scheduleOptions, ctx);
        return true;
    }
}
