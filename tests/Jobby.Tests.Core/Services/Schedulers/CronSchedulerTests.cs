using Moq;

using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Services.Schedulers.Cron;
using Jobby.Core.Models.Schedulers;

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
        var scheduler = new CronScheduler(schedule, calculateNextFromPrev);
        var next = scheduler.GetFirstStartTime(now);

        var expectedNext = CronHelper.Parse(schedule).GetNext(now);
        Assert.Equal(expectedNext, next);
    }

    [Fact]
    public void PrevSpecified_ConfiguredToCalculateFromNow_CalculatesNextFromNow()
    {
        var now = DateTime.UtcNow;
        _timerService.Setup(x => x.GetUtcNow()).Returns(now);

        var schedule = "*/5 * * * *";
        var scheduler = new CronScheduler(schedule, calculateNextFromPrev: false);
        var next = scheduler.GetNextStartTime(new SchedulerExecutionContext(now, now.AddHours(-1)));

        var expectedNext = CronHelper.Parse(schedule).GetNext(now);
        Assert.Equal(expectedNext, next);
    }

    [Fact]
    public void PrevSpecified_ConfiguredToCalculateFromPrev_CalculatesNextFromPrev()
    {
        var now = DateTime.UtcNow;
        _timerService.Setup(x => x.GetUtcNow()).Returns(now);

        var schedule = "*/5 * * * *";
        var scheduler = new CronScheduler(schedule, calculateNextFromPrev: true);
        var prev = now.AddHours(-1);
        var next = scheduler.GetNextStartTime(new SchedulerExecutionContext(now, prev));

        var expectedNext = CronHelper.Parse(schedule).GetNext(prev);
        Assert.Equal(expectedNext, next);
    }
}
