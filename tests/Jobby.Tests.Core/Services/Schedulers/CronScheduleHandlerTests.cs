using Jobby.Core.Exceptions;
using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Services.Schedulers;
using Moq;

namespace Jobby.Tests.Core.Services.Schedulers;

public class CronScheduleHandlerTests
{
    private readonly CronScheduleHandler _scheduleHandler = new();

    [Fact]
    public void GetFirstStartTime_CalculatesFirstStartTimeFromNow()
    {
        var now = DateTime.UtcNow;
        var schedule = new CronSchedule { CronExpression =  "*/5 * * * *" };
        
        var startTime = _scheduleHandler.GetFirstStartTime(schedule, now);
        
        var expected = CronHelper.GetNext(schedule.CronExpression, now);
        Assert.Equal(expected, startTime);
    }

    [Fact]
    public void GetNextStartTime_CalculateNextFromPrevFalse_CalculatesNextFromNow()
    {
        var ctx = new ScheduleCalculationContext
        {
            UtcNow = DateTime.UtcNow,
            PrevScheduledTime = DateTime.UtcNow.AddHours(-1),
        };
        var schedule = new  CronSchedule
        {
            CronExpression = "*/5 * * * *",
            CalculateNextFromPrev = false
        };
        
        var startTime = _scheduleHandler.GetNextStartTime(schedule, ctx);
        
        var expected = CronHelper.GetNext(schedule.CronExpression, ctx.UtcNow);
        Assert.Equal(expected, startTime);
    }
    
    [Fact]
    public void GetNextStartTime_CalculateNextFromPrevTrue_CalculatesNextFromPrev()
    {
        var ctx = new ScheduleCalculationContext
        {
            UtcNow = DateTime.UtcNow,
            PrevScheduledTime = DateTime.UtcNow.AddHours(-1),
        };
        var schedule = new  CronSchedule
        {
            CronExpression = "*/5 * * * *",
            CalculateNextFromPrev = true
        };
        
        var startTime = _scheduleHandler.GetNextStartTime(schedule, ctx);
        
        var expected = CronHelper.GetNext(schedule.CronExpression, ctx.PrevScheduledTime);
        Assert.Equal(expected, startTime);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a cron")]
    [InlineData("*/5 * * *")]
    [InlineData("*/5 * * * * * *")]
    public void EnsureScheduleIsValid_InvalidCronExpression_Throws(string cronExpression)
    {
        var schedule = new CronSchedule { CronExpression = cronExpression };

        Assert.Throws<InvalidScheduleException>(() => _scheduleHandler.EnsureScheduleIsValid(schedule));
    }

    [Fact]
    public void EnsureScheduleIsValid_ValidCronExpression_DoesNotThrow()
    {
        var schedule = new CronSchedule { CronExpression = "*/5 * * * *" };

        _scheduleHandler.EnsureScheduleIsValid(schedule);
    }

    [Fact]
    public void SerializeAndDeserialize_CronExprWithoutOptions_SerializesAndDesiralizesCorrectly()
    {
        var schedule = new CronSchedule
        {
            CronExpression = "*/5 * * * *",
        };
        
        var serialized = _scheduleHandler.SerializeSchedule(schedule);
        var deserialized = _scheduleHandler.DeserializeSchedule(serialized);
        
        Assert.Equal(schedule.CronExpression, serialized);
        Assert.Equal(schedule.CronExpression, deserialized.CronExpression);
        Assert.False(deserialized.CalculateNextFromPrev);
    }
    
    [Fact]
    public void SerializeAndDeserialize_WithOptions_SerializesAndDesiralizesCorrectly()
    {
        var schedule = new CronSchedule
        {
            CronExpression = "*/5 * * * *",
            CalculateNextFromPrev = true
        };

        var serialized = _scheduleHandler.SerializeSchedule(schedule);
        var deserialized = _scheduleHandler.DeserializeSchedule(serialized);

        Assert.Equal(schedule.CronExpression, deserialized.CronExpression);
        Assert.True(deserialized.CalculateNextFromPrev);
    }

    [Fact]
    public void SerializeSchedule_CalculateNextFromPrevTrue_SerializesToJsonFormat()
    {
        var schedule = new CronSchedule
        {
            CronExpression = "*/5 * * * *",
            CalculateNextFromPrev = true
        };

        var serialized = _scheduleHandler.SerializeSchedule(schedule);

        Assert.StartsWith("{", serialized);
        Assert.Contains(schedule.CronExpression, serialized);
        Assert.Contains(@"""CalculateNextFromPrev"":true", serialized);
    }

    [Fact]
    public void SerializeSchedule_CalculateNextFromPrevFalse_SerializesAsPlainCronWithoutJson()
    {
        var schedule = new CronSchedule
        {
            CronExpression = "*/5 * * * *",
            CalculateNextFromPrev = false
        };

        var serialized = _scheduleHandler.SerializeSchedule(schedule);

        Assert.Equal("*/5 * * * *", serialized);
    }

    [Fact]
    public void DeserializeSchedule_LegacyPlainCron_ReturnsScheduleWithoutCalculateNextFromPrev()
    {
        // Legacy format: plain cron string stored before CalculateNextFromPrev option was introduced
        var legacySchedule = "*/5 * * * *";

        var deserialized = _scheduleHandler.DeserializeSchedule(legacySchedule);

        Assert.Equal("*/5 * * * *", deserialized.CronExpression);
        Assert.False(deserialized.CalculateNextFromPrev);
    }

    [Fact]
    public void DeserializeSchedule_JsonWithCalculateNextFromPrevTrue_ParsesSchedule()
    {
        var json = @"{""CronExpression"":""*/5 * * * *"",""CalculateNextFromPrev"":true}";

        var deserialized = _scheduleHandler.DeserializeSchedule(json);

        Assert.Equal("*/5 * * * *", deserialized.CronExpression);
        Assert.True(deserialized.CalculateNextFromPrev);
    }

    [Fact]
    public void DeserializeSchedule_JsonWithCalculateNextFromPrevFalse_ParsesSchedule()
    {
        var json = @"{""CronExpression"":""*/5 * * *"",""CalculateNextFromPrev"":false}";

        var deserialized = _scheduleHandler.DeserializeSchedule(json);

        Assert.Equal("*/5 * * *", deserialized.CronExpression);
        Assert.False(deserialized.CalculateNextFromPrev);
    }

    [Fact]
    public void DeserializeSchedule_InvalidJson_ThrowsInvalidScheduleException()
    {
        var json = @"{""CronExpression"":";

        Assert.Throws<InvalidScheduleException>(() => _scheduleHandler.DeserializeSchedule(json));
    }
}