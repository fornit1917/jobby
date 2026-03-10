using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models.Schedulers;
using Jobby.Core.Services.Schedulers.Storages;

namespace Jobby.Samples.AspNet.Schedulers;

public class CustomSecondsScheduler : IScheduler
{
    public uint Seconds { get; init; }

    public DateTime GetFirstStartTime(DateTime utcNow) => utcNow;

    public DateTime GetNextStartTime(in SchedulerExecutionContext ctx)
    {
        return ctx.PreviousScheduledStartTime.AddSeconds(Seconds > 0 ? Seconds : 1);
    }
}

public class CustomSecondsStorage : BaseSchedulerStorage<CustomSecondsScheduler>
{
    public override string DefaultSchedulerType => "CUSTOM_SECONDS";
}