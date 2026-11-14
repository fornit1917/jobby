using System.Diagnostics;
using Jobby.Core.Services;

namespace Jobby.Tests.Core.Services;

public class TimerServiceTests
{
    [Fact]
    public async Task Delay_WithMilliseconds_CancellationTokenCancelled_DoesNotThrow()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // must not throw OperationCanceledException even though cancellation hits before the delay ends
        await TimerService.Instance.Delay(60_000, cts.Token);
    }

    [Fact]
    public async Task Delay_WithTimeSpan_CancellationTokenCancelled_DoesNotThrow()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await TimerService.Instance.Delay(TimeSpan.FromMinutes(1), cts.Token);
    }

    [Fact]
    public async Task Delay_CompletesAfterSpecifiedMilliseconds()
    {
        var stopwatch = Stopwatch.StartNew();

        await TimerService.Instance.Delay(100, TestContext.Current.CancellationToken);

        stopwatch.Stop();
        Assert.True(stopwatch.ElapsedMilliseconds >= 100, $"Elapsed only {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void GetCurrentTicks_CompatibleWithStopwatch()
    {
        var ticks = TimerService.Instance.GetCurrentTicks();

        // MultiQueueService mixes ITimerService.GetCurrentTicks with Stopwatch-based GetElapsedTime,
        // so ticks must be interchangeable with Stopwatch.GetTimestamp
        var elapsed = Stopwatch.GetElapsedTime(ticks);

        Assert.True(elapsed >= TimeSpan.Zero);
        Assert.True(elapsed < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetElapsedTime_ReturnsGrowingElapsedFromTicks()
    {
        var start = TimerService.Instance.GetCurrentTicks();

        Thread.Sleep(50);

        var elapsed = TimerService.Instance.GetElapsedTime(start);
        Assert.True(elapsed.TotalMilliseconds >= 50, $"Elapsed only {elapsed.TotalMilliseconds}ms");
    }

    [Fact]
    public void GetUtcNow_ReturnsUtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-5);
        var now = TimerService.Instance.GetUtcNow();
        var after = DateTime.UtcNow.AddSeconds(5);

        Assert.InRange(now, before, after);
        Assert.Equal(DateTimeKind.Utc, now.Kind);
    }
}
