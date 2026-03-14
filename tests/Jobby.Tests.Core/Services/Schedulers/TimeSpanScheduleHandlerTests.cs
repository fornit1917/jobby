using Jobby.Core.Exceptions;
using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Services.Schedulers;
using Moq;

namespace Jobby.Tests.Core.Services.Schedulers;

public class TimeSpanScheduleHandlerTests
{
    private readonly TimeSpanScheduleHandler _scheduleHandler = new();

    [Fact]
    public void GetFirstStartTime_ReturnsUtcNow()
    {
        var schedule = new TimeSpanSchedule { Interval = TimeSpan.FromHours(1) };
        var utcNow = DateTime.UtcNow;
        
        var startTime = _scheduleHandler.GetFirstStartTime(schedule, utcNow);
        
        Assert.Equal(utcNow, startTime);
    }

    [Fact]
    public void GetNextStartTime_CalculateNextFromPrevFalse_CalculatesNextFromNow()
    {
        var schedule = new TimeSpanSchedule
        {
            Interval = TimeSpan.FromHours(1),
            CalculateNextFromPrev = false,
        };
        var ctx = new ScheduleCalculationContext
        {
            PrevScheduledTime = DateTime.UtcNow.AddHours(-1),
            UtcNow = DateTime.UtcNow,
        };
        
        var startTime = _scheduleHandler.GetNextStartTime(schedule, ctx);
        
        Assert.Equal(ctx.UtcNow.Add(schedule.Interval), startTime);
    }
    
    [Fact]
    public void GetNextStartTime_CalculateNextFromPrevTrue_CalculatesNextFromPrev()
    {
        var schedule = new TimeSpanSchedule
        {
            Interval = TimeSpan.FromHours(1),
            CalculateNextFromPrev = true,
        };
        var ctx = new ScheduleCalculationContext
        {
            PrevScheduledTime = DateTime.UtcNow.AddHours(-1),
            UtcNow = DateTime.UtcNow,
        };
        
        var startTime = _scheduleHandler.GetNextStartTime(schedule, ctx);
        
        Assert.Equal(ctx.PrevScheduledTime.Add(schedule.Interval), startTime);
    }

    [Fact]
    public void GetNextStartTime_IntervalNegative_Throws()
    {
        var schedule = new TimeSpanSchedule
        {
            Interval = TimeSpan.FromHours(-1),
        };
        var ctx = new ScheduleCalculationContext
        {
            PrevScheduledTime = DateTime.UtcNow.AddHours(-1),
            UtcNow = DateTime.UtcNow,
        };
        
        Assert.Throws<InvalidScheduleException>(() => _scheduleHandler.GetNextStartTime(schedule, ctx));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SerializeAndDeserialize_SerializesAndDesiralizesCorrectly(bool calculateNextFromPrev)
    {
        var schedule = new TimeSpanSchedule
        {
            Interval = TimeSpan.FromHours(1),
            CalculateNextFromPrev = calculateNextFromPrev,
        };
        
        var serialized = _scheduleHandler.SerializeSchedule(schedule);
        var deserialized = _scheduleHandler.DeserializeSchedule(serialized);
        
        Assert.Equal(schedule.Interval, deserialized.Interval);
        Assert.Equal(schedule.CalculateNextFromPrev, deserialized.CalculateNextFromPrev);
    }
}