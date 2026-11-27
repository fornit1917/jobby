using Jobby.Dashboard.UI.Shared;

namespace Jobby.Tests.Dashboard;

// Pin test: cron schedule descriptions are always English by design. They do not follow the language
// selected in the dashboard and must not depend on the culture of the machine running the code, which
// is what the exact assertions below lock in.
public class ScheduleFormatterTests
{
    [Theory]
    [InlineData("0 */2 * * *", "Every 2 hours")]
    [InlineData("  0 */2 * * *  ", "Every 2 hours")]
    [InlineData("every morning", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void DescribeCron_DescribesOnlyParsableExpressions(string? cron, string? expected)
    {
        Assert.Equal(expected, ScheduleFormatter.DescribeCron(cron));
    }

    [Fact]
    public void Bare_CronDailyProducesDescriptionAndBadge()
    {
        var model = ScheduleFormatter.Format("0 0 9 * * *", "JOBBY_CRON");
        Assert.Equal(ScheduleKind.Cron, model.Kind);
        Assert.Equal("0 0 9 * * *", model.Badge);
        Assert.Equal("At 09:00 AM", model.Description);
    }

    [Fact]
    public void Bare_CronFiveMinutesIsDescribed()
    {
        var model = ScheduleFormatter.Format("*/5 * * * *", "JOBBY_CRON");
        Assert.Equal(ScheduleKind.Cron, model.Kind);
        Assert.Equal("Every 5 minutes", model.Description);
    }

    [Fact]
    public void Six_FieldCronSecondsIsDescribed()
    {
        var model = ScheduleFormatter.Format("*/5 * * * * *", "JOBBY_CRON");
        Assert.Equal(ScheduleKind.Cron, model.Kind);
        Assert.Equal("Every 5 seconds", model.Description);
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
