using Jobby.Core.Models.Schedulers;

namespace Jobby.Core.Interfaces.Schedulers;

internal interface ISchedulerExecutor
{
    bool TryGetNextStartTime(string schedule, in SchedulerExecutionContext ctx, out DateTime nextStartTime);
}
