using Jobby.Core.Models.Schedulers;

namespace Jobby.Core.Interfaces.Schedulers;

internal interface IScheduleExecutor
{
    bool TryGetNextStartTime(string schedule, in SchedulerExecutionContext ctx, IJobParamSerializer defaultSerailizer, out DateTime nextStartTime);
}
