using Jobby.Dashboard.Client.Shared;

namespace Jobby.Tests.Dashboard;

public class ScheduleFormatterTests
{
    [Theory]
    [InlineData(null, "JOBBY_CRON")]
    [InlineData("", "JOBBY_CRON")]
    [InlineData("   ", "JOBBY_INTERVAL")]
    public void Empty_ScheduleIsKindNone(string? schedule, string? schedulerType)
    {
        var model = ScheduleFormatter.Format(schedule, schedulerType);
        Assert.Equal(ScheduleKind.None, model.Kind);
        Assert.Null(model.Description);
        Assert.Null(model.Badge);
    }

    [Fact]
    public void Bare_CronDailyProducesDescriptionAndBadge()
    {
        var model = ScheduleFormatter.Format("0 0 9 * * *", "JOBBY_CRON");
        Assert.Equal(ScheduleKind.Cron, model.Kind);
        Assert.Equal("0 0 9 * * *", model.Badge);
        Assert.NotNull(model.Description);
        Assert.Contains("09:00", model.Description);
    }

    [Fact]
    public void Bare_CronFiveMinutesIsDescribed()
    {
        var model = ScheduleFormatter.Format("*/5 * * * *", "JOBBY_CRON");
        Assert.Equal(ScheduleKind.Cron, model.Kind);
        Assert.Contains("5 minutes", model.Description);
    }

    [Fact]
    public void Six_FieldCronSecondsIsDescribed()
    {
        var model = ScheduleFormatter.Format("*/5 * * * * *", "JOBBY_CRON");
        Assert.Equal(ScheduleKind.Cron, model.Kind);
        Assert.Contains("5 seconds", model.Description);
    }

    [Fact]
    public void Cron_JsonFormExtractsExpression()
    {
        var model = ScheduleFormatter.Format(
            """{"CronExpression":"0 0 9 * * *","CalculateNextFromPrev":true}""", "JOBBY_CRON");
        Assert.Equal(ScheduleKind.Cron, model.Kind);
        Assert.Equal("0 0 9 * * *", model.Badge);
        Assert.Contains("09:00", model.Description);
    }

    [Fact]
    public void Invalid_CronFallsBackToBadgeOnly()
    {
        var model = ScheduleFormatter.Format("definitely not cron", "JOBBY_CRON");
        Assert.Equal(ScheduleKind.Cron, model.Kind);
        Assert.Equal("definitely not cron", model.Badge);
        Assert.Null(model.Description);
    }

    [Fact]
    public void Interval_SecondsIsHumanized()
    {
        var model = ScheduleFormatter.Format(
            """{"Interval":"00:00:45","CalculateNextFromPrev":false}""", "JOBBY_INTERVAL");
        Assert.Equal(ScheduleKind.Interval, model.Kind);
        Assert.Equal("Every 45 seconds", model.Description);
        Assert.Equal("00:00:45", model.Badge);
    }

    [Fact]
    public void Interval_MinutesIsHumanized()
    {
        var model = ScheduleFormatter.Format("""{"Interval":"00:05:00"}""", "JOBBY_INTERVAL");
        Assert.Equal("Every 5 minutes", model.Description);
        Assert.Equal("00:05:00", model.Badge);
    }

    [Fact]
    public void Interval_CompoundIsHumanized()
    {
        var model = ScheduleFormatter.Format("""{"Interval":"01:30:00"}""", "JOBBY_INTERVAL");
        Assert.Equal("Every 1 hour 30 minutes", model.Description);
        Assert.Equal("01:30:00", model.Badge);
    }

    [Fact]
    public void Interval_DayIsSingular()
    {
        var model = ScheduleFormatter.Format("""{"Interval":"1.00:00:00"}""", "JOBBY_INTERVAL");
        Assert.Equal("Every 1 day", model.Description);
        Assert.Equal("1.00:00:00", model.Badge);
    }

    [Fact]
    public void Interval_SubsecondIsHumanizedInMilliseconds()
    {
        var model = ScheduleFormatter.Format("""{"Interval":"00:00:00.2500000"}""", "JOBBY_INTERVAL");
        Assert.Equal(ScheduleKind.Interval, model.Kind);
        Assert.Equal("Every 250 milliseconds", model.Description);
    }

    [Fact]
    public void Interval_SubmillisecondIsNotRenderedAsZero()
    {
        var model = ScheduleFormatter.Format("""{"Interval":"00:00:00.0000001"}""", "JOBBY_INTERVAL");
        Assert.Equal(ScheduleKind.Interval, model.Kind);
        Assert.Equal("Every <1 millisecond", model.Description);
        Assert.Equal("00:00:00.0000001", model.Badge);
    }

    [Fact]
    public void Interval_NegativeFallsBackToBadgeOnly()
    {
        var model = ScheduleFormatter.Format("""{"Interval":"-00:00:01"}""", "JOBBY_INTERVAL");
        Assert.Equal(ScheduleKind.Interval, model.Kind);
        Assert.Null(model.Description);
        Assert.Equal("-00:00:01", model.Badge);
    }

    [Fact]
    public void Interval_UnparseableFallsBackToBadgeOnly()
    {
        var model = ScheduleFormatter.Format("""{"foo":1}""", "JOBBY_INTERVAL");
        Assert.Equal(ScheduleKind.Interval, model.Kind);
        Assert.Null(model.Description);
        Assert.Equal("""{"foo":1}""", model.Badge);
    }

    [Fact]
    public void Custom_SchedulerShowsRawBadgeNoDescription()
    {
        var model = ScheduleFormatter.Format("""{"SecondsInterval":45}""", "CUSTOM_SECONDS_INTERVAL");
        Assert.Equal(ScheduleKind.Custom, model.Kind);
        Assert.Null(model.Description);
        Assert.Equal("""{"SecondsInterval":45}""", model.Badge);
    }

    [Fact]
    public void Null_SchedulerTypeWithValueIsCustom()
    {
        var model = ScheduleFormatter.Format("whatever", null);
        Assert.Equal(ScheduleKind.Custom, model.Kind);
        Assert.Equal("whatever", model.Badge);
    }
}