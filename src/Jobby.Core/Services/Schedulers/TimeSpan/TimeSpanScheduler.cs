using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models.Schedulers;

namespace Jobby.Core.Services.Schedulers.TimeSpans;

internal class TimeSpanScheduler : IScheduler
{
    public readonly TimeSpan Interval;
    public readonly bool CalculateNextFromPrev;

    public TimeSpanScheduler(TimeSpan interval, bool calculateNextFromPrev)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentException($"{nameof(interval)} should be more than zero");

        Interval = interval;
        CalculateNextFromPrev = calculateNextFromPrev;
    }

    public DateTime GetFirstStartTime(DateTime utcNow) => utcNow;

    public DateTime GetNextStartTime(in SchedulerExecutionContext ctx)
    {       
        var from = CalculateNextFromPrev ?
            ctx.PreviousScheduledStartTime :
            ctx.UtcNow;
        
        return from.Add(Interval);
    }
}