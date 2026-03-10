using Jobby.Core.Models.Schedulers;

namespace Jobby.Core.Interfaces.Schedulers;

public interface IScheduler
{
    DateTime GetFirstStartTime(DateTime utcNow);
    DateTime GetNextStartTime(in SchedulerExecutionContext ctx);
}
