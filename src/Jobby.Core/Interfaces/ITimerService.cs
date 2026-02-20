namespace Jobby.Core.Interfaces;

internal interface ITimerService
{
    Task Delay(int milliseconds);
    long GetCurrentTicks();
    TimeSpan GetElapsedTime(long startTicks);
    DateTime GetUtcNow();
}