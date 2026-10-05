using System.Globalization;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client;

namespace Jobby.Tests.Dashboard.Globalization;

public class DateTimeFormatPatternsTests
{
    [Theory]
    [InlineData(DashboardDateStyle.Iso, "2026-06-10")]
    [InlineData(DashboardDateStyle.DayFirstSlash, "10/06/2026")]
    [InlineData(DashboardDateStyle.DayFirstDot, "10.06.2026")]
    [InlineData(DashboardDateStyle.MonthFirstSlash, "06/10/2026")]
    public void Date_PatternsRenderExpected(DashboardDateStyle style, string expected)
    {
        var sample = new DateTime(2026, 6, 10, 14, 30, 5);
        Assert.Equal(expected,
            sample.ToString(DateTimeFormatPatterns.DatePattern(style), CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(DashboardTimeStyle.H24, "14:30")]
    [InlineData(DashboardTimeStyle.H24WithSeconds, "14:30:05")]
    [InlineData(DashboardTimeStyle.H12, "2:30 PM")]
    [InlineData(DashboardTimeStyle.H12WithSeconds, "2:30:05 PM")]
    public void Time_PatternsRenderExpected(DashboardTimeStyle style, string expected)
    {
        var sample = new DateTime(2026, 6, 10, 14, 30, 5);
        Assert.Equal(expected,
            sample.ToString(DateTimeFormatPatterns.TimePattern(style), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Combined_DefaultMatchesTodaysFormat()
    {
        var sample = new DateTime(2026, 6, 10, 14, 30, 5);
        var s = DateTimeFormatPatterns.Format(sample, DashboardDateStyle.Iso, DashboardTimeStyle.H24WithSeconds);
        Assert.Equal("2026-06-10 14:30:05", s);
    }
}