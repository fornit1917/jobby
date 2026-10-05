using System.Globalization;
using Jobby.Dashboard.Client.Shared;

namespace Jobby.Tests.Dashboard.Globalization;

public class InvariantGlobalizationScheduleTests
{
    [Fact]
    public void Invariant_GlobalizationIsActive()
    {
        Assert.Equal(string.Empty, CultureInfo.CurrentCulture.Name);
        Assert.Equal(string.Empty, CultureInfo.CurrentUICulture.Name);
    }

    [Fact]
    public void Cron_DescriptionIsProducedUnderInvariantGlobalization()
    {
        var model = ScheduleFormatter.Format("0 0 9 * * *", "JOBBY_CRON");

        Assert.Equal(ScheduleKind.Cron, model.Kind);
        Assert.False(string.IsNullOrEmpty(model.Description),
            "Cron description must be generated under InvariantGlobalization - a non-empty Locale makes the " +
            "describer call new CultureInfo(locale), which throws here and silently drops the description.");
        Assert.Contains("09:00", model.Description);
    }

    [Fact]
    public void Interval_DescriptionIsProducedUnderInvariantGlobalization()
    {
        var model = ScheduleFormatter.Format("""{"Interval":"00:00:45"}""", "JOBBY_INTERVAL");

        Assert.Equal(ScheduleKind.Interval, model.Kind);
        Assert.Equal("Every 45 seconds", model.Description);
    }
}
