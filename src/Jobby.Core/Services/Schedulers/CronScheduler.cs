using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Core.Services.Schedulers;

internal class CronScheduler : ISchedule
{
    private readonly ITimerService _timerService;
    private readonly bool _calculateNextFromPrev;

    public CronScheduler(ITimerService timerService, bool calculateNextFromPrev = false)
    {
        _timerService = timerService;
        _calculateNextFromPrev = calculateNextFromPrev;
    }

    public DateTime GetNextStartTime(string schedule, DateTime? previousScheduledStartTime)
    {
        var from = previousScheduledStartTime.HasValue && _calculateNextFromPrev 
            ? previousScheduledStartTime.Value
            : _timerService.GetUtcNow();
        
        return CronHelper.GetNext(schedule, from);
    }
}