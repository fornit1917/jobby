using Jobby.Core.Exceptions;
using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Models.Schedulers;
using Jobby.Core.Services.Schedulers;
using Jobby.Core.Services.Schedulers.TimeSpans;
using Moq;

namespace Jobby.Tests.Core.Services.Schedulers;

public class TimeSpanSchedulerTests
{
    private readonly Mock<ITimerService> _timerService = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PrevNotSpecified_CalculatesNextFromNow(bool calculateNextFromPrev)
    {
        var now = DateTime.UtcNow;
        _timerService.Setup(x => x.GetUtcNow()).Returns(now);

        var scheduler = new TimeSpanScheduler(TimeSpan.FromSeconds(5), calculateNextFromPrev);
        var next = scheduler.GetFirstStartTime(now);

        Assert.Equal(now, next);
    }

    [Fact]
    public void PrevSpecified_ConfiguredToCalculateFromNow_CalculatesNextFromNow()
    {
        var now = DateTime.UtcNow;
        _timerService.Setup(x => x.GetUtcNow()).Returns(now);

        var scheduler = new TimeSpanScheduler(TimeSpan.FromSeconds(5), calculateNextFromPrev: false);
        var next = scheduler.GetNextStartTime(new SchedulerExecutionContext(now, now.AddHours(-1)));

        var expectedNext = now.AddSeconds(5);
        Assert.Equal(expectedNext, next);
    }

    [Fact]
    public void PrevSpecified_ConfiguredToCalculateFromPrev_CalculatesNextFromPrev()
    {
        var now = DateTime.UtcNow;
        _timerService.Setup(x => x.GetUtcNow()).Returns(now);

        var scheduler = new TimeSpanScheduler(TimeSpan.FromSeconds(5), calculateNextFromPrev: true);
        var prev = DateTime.UtcNow.AddHours(-1);
        var next = scheduler.GetNextStartTime(new SchedulerExecutionContext(now, prev));

        var expectedNext = prev.AddSeconds(5);
        Assert.Equal(expectedNext, next);
    }

    [Fact]
    public void NegativeInterval_Throws()
    {
        var testCode = () => new TimeSpanScheduler(TimeSpan.FromSeconds(-5), calculateNextFromPrev: false);

        Assert.Throws<ArgumentException>(testCode);
    }
}