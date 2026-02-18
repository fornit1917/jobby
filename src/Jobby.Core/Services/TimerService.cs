using System.Diagnostics;
using Jobby.Core.Interfaces;

namespace Jobby.Core.Services;

internal class TimerService : ITimerService
{
    public static readonly ITimerService Instance = new TimerService();
    
    public Task Delay(int milliseconds)
    {
        return Task.Delay(milliseconds);
    }

    public long GetCurrentTicks()
    {
        return Stopwatch.GetTimestamp();
    }

    public TimeSpan GetElapsedTime(long startTicks)
    {
        return Stopwatch.GetElapsedTime(startTicks);
    }

    public DateTime GetUtcNow()
    {
        return DateTime.UtcNow;
    }
}