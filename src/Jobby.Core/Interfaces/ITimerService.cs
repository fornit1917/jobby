namespace Jobby.Core.Interfaces;

internal interface ITimerService
{
    Task Delay(int milliseconds, CancellationToken cancellationToken = default);
    Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken = default);
    long GetCurrentTicks();
    TimeSpan GetElapsedTime(long startTicks);
    DateTime GetUtcNow();
}