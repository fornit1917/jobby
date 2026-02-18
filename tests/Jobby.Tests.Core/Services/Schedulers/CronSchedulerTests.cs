using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Services.Schedulers;
using Moq;

namespace Jobby.Tests.Core.Services.Schedulers;

public class CronSchedulerTests
{
    private readonly Mock<ITimerService> _timerService = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PrevNotSpecified_CalculatesNextFromNow(bool calculateNextFromPrev)
    {
        var now = DateTime.UtcNow;
        _timerService.Setup(x => x.GetUtcNow()).Returns(now);
        
        var schedule = "*/5 * * * *";
        var scheduler = new CronScheduler(_timerService.Object, calculateNextFromPrev);
        var next = scheduler.GetNextStartTime(schedule, null);
        
        var expectedNext = CronHelper.GetNext(schedule, now);
        Assert.Equal(expectedNext, next);
    }
    
    [Fact]
    public void PrevSpecified_ConfiguredToCalculateFromNow_CalculatesNextFromNow()
    {
        var now = DateTime.UtcNow;
        _timerService.Setup(x => x.GetUtcNow()).Returns(now);
        
        var schedule = "*/5 * * * *";
        var scheduler = new CronScheduler(_timerService.Object, calculateNextFromPrev: false);
        var next = scheduler.GetNextStartTime(schedule, DateTime.UtcNow.AddHours(-1));
        
        var expectedNext = CronHelper.GetNext(schedule, now);
        Assert.Equal(expectedNext, next);
    }
    
    [Fact]
    public void PrevSpecified_ConfiguredToCalculateFromPrev_CalculatesNextFromPrev()
    {
        var now = DateTime.UtcNow;
        _timerService.Setup(x => x.GetUtcNow()).Returns(now);
        
        var schedule = "*/5 * * * *";
        var scheduler = new CronScheduler(_timerService.Object, calculateNextFromPrev: true);
        var prev = DateTime.UtcNow.AddHours(-1);
        var next = scheduler.GetNextStartTime(schedule, prev);
        
        var expectedNext = CronHelper.GetNext(schedule, prev);
        Assert.Equal(expectedNext, next);
    }
}