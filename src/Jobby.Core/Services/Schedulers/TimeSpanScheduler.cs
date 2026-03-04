using System.Globalization;
using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Core.Services.Schedulers;

internal class TimeSpanScheduler : IScheduler
{
    private readonly ITimerService _timerService;
    private readonly bool _calculateNextFromPrev;

    public TimeSpanScheduler(ITimerService timerService, bool calculateNextFromPrev)
    {
        _timerService = timerService;
        _calculateNextFromPrev = calculateNextFromPrev;
    }

    public DateTime GetNextStartTime(string schedule, DateTime? previousScheduledStartTime)
    {
        var span = TimeSpan.Parse(schedule, CultureInfo.InvariantCulture);
        if (span < TimeSpan.Zero)
        {
            throw new InvalidScheduleException(
                $"Interval for TimeSpanScheduler can not be negative, but '{schedule}' is");
        }
        
        var from = previousScheduledStartTime.HasValue && _calculateNextFromPrev
            ? previousScheduledStartTime.Value
            : _timerService.GetUtcNow();
        
        return from.Add(span);
    }
}