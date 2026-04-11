using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Core.Services.Schedulers;

public class TimeSpanSchedule : ISchedule
{
    public TimeSpan Interval { get; init; }
    public bool CalculateNextFromPrev { get; init; }
}