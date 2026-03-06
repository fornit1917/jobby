using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models;
using Jobby.Core.Models.Schedulers;

namespace Jobby.Core.Services.Schedulers;
internal class ScheduleExecutor<TSchedule> : IScheduleExecutor
    where TSchedule : ISchedule
{
    public readonly IScheduleHandler<TSchedule> ScheduleHandler;
    public readonly IJobParamSerializer<TSchedule>? ScheduleSerializer;

    public ScheduleExecutor(IScheduleHandler<TSchedule> scheduleHandler, IJobParamSerializer<TSchedule>? scheduleSerializer = null)
    {
        ScheduleHandler = scheduleHandler ?? throw new ArgumentNullException(nameof(scheduleHandler));
        ScheduleSerializer = scheduleSerializer;
    }

    bool IScheduleExecutor.TryGetNextStartTime(string schedule, in SchedulerExecutionContext ctx, IJobParamSerializer defaultSerailizer, out DateTime nextStartTime)
    {
        var serializer = ScheduleSerializer ?? new DefaultJobParamSerializer<TSchedule>(defaultSerailizer);

        if (!serializer.TryDeserializeJobParam(schedule, out var scheduleOptions))
        {
            nextStartTime = default;
            return false;
        }

        nextStartTime = ScheduleHandler.GetNextStartTime(scheduleOptions, ctx);
        return true;
    }
}
