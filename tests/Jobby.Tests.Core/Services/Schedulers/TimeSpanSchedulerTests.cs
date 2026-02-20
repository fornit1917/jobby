using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Services.Schedulers;
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
        
        var schedule = "00:00:05";
        var scheduler = new TimeSpanScheduler(_timerService.Object, calculateNextFromPrev);
        var next = scheduler.GetNextStartTime(schedule, null);
        
        var expectedNext = now.AddSeconds(5);
        Assert.Equal(expectedNext, next);
    }
    
    [Fact]
    public void PrevSpecified_ConfiguredToCalculateFromNow_CalculatesNextFromNow()
    {
        var now = DateTime.UtcNow;
        _timerService.Setup(x => x.GetUtcNow()).Returns(now);
        
        var schedule = "00:00:05";
        var scheduler = new TimeSpanScheduler(_timerService.Object, calculateNextFromPrev: false);
        var next = scheduler.GetNextStartTime(schedule, DateTime.UtcNow.AddHours(-1));
        
        var expectedNext = now.AddSeconds(5);
        Assert.Equal(expectedNext, next);
    }
    
    [Fact]
    public void PrevSpecified_ConfiguredToCalculateFromPrev_CalculatesNextFromPrev()
    {
        var now = DateTime.UtcNow;
        _timerService.Setup(x => x.GetUtcNow()).Returns(now);
        
        var schedule = "00:00:05";
        var scheduler = new TimeSpanScheduler(_timerService.Object, calculateNextFromPrev: true);
        var prev = DateTime.UtcNow.AddHours(-1);
        var next = scheduler.GetNextStartTime(schedule, prev);
        
        var expectedNext = prev.AddSeconds(5);
        Assert.Equal(expectedNext, next);
    }
}