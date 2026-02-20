using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Samples.AspNet.Schedulers;

public class CustomSecondsScheduler : IScheduler
{
    public const string Name = "CUSTOM_SECONDS";
    
    public DateTime GetNextStartTime(string schedule, DateTime? previousScheduledStartTime)
    {
        var seconds = int.Parse(schedule);
        return previousScheduledStartTime?.AddSeconds(seconds) ?? DateTime.UtcNow;
    }
}