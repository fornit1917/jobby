using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models;
using Jobby.Core.Models.Schedulers;

namespace Jobby.Core.Services.Schedulers;
internal class ScheduleExecutor<TSchedule> : IScheduleExecutor
    where TSchedule : ISchedule
{
    private readonly IScheduleHandler<TSchedule> _scheduleHandler;
    private readonly IJobParamSerializer<TSchedule>? _scheduleSerializer;

    public ScheduleExecutor(IScheduleHandler<TSchedule> scheduleHandler, IJobParamSerializer<TSchedule>? scheduleSerializer = null)
    {
        _scheduleHandler = scheduleHandler ?? throw new ArgumentNullException(nameof(scheduleHandler));
        _scheduleSerializer = scheduleSerializer);
    }

    public bool TryGetNextStartTime(string schedule, in SchedulerExecutionContext ctx, IJobParamSerializer defaultSerailizer, out DateTime nextStartTime)
    {
        var serializer = _scheduleSerializer ?? new DefaultJobParamSerializer<TSchedule>(defaultSerailizer);

        if (!serializer.TryDeserializeJobParam(schedule, out var scheduleOptions))
        {
            nextStartTime = default;
            return false;
        }

        nextStartTime = _scheduleHandler.GetNextStartTime(scheduleOptions, ctx);
        return true;
    }
}
