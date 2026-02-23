using System.Diagnostics;
using Jobby.Core.Interfaces;

namespace Jobby.Core.Services;

internal class TimerService : ITimerService
{
    public static readonly ITimerService Instance = new TimerService();
    
    public async Task Delay(int milliseconds, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(milliseconds, cancellationToken);
        }
        catch (TaskCanceledException)
        {
        }
    }

    public async Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(timeSpan, cancellationToken);
        }
        catch (TaskCanceledException)
        {
        }
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