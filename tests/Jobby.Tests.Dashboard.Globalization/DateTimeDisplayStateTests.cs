using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client;

namespace Jobby.Tests.Dashboard.Globalization;

public class DateTimeDisplayStateTests
{
    private static DateTimeDisplayState StateWith(string? tzId, DashboardDateStyle date, DashboardTimeStyle time)
    {
        var config = new DashboardClientConfig();
        var state = new DateTimeDisplayState(config);
        state.ApplyForTests(tzId, date, time);
        return state;
    }

    [Fact]
    public void Engine_AssumptionTimezoneDatabaseIsAvailableUnderInvariantGlobalization()
    {
        Assert.NotEmpty(TimeZoneInfo.GetSystemTimeZones());
        Assert.True(TimeZoneInfo.TryFindSystemTimeZoneById("Europe/London", out _));
    }

    [Fact]
    public void FormatAbsolute_ConvertsToTimezoneAndUsesPreset()
    {
        var state = StateWith("Europe/London", DashboardDateStyle.DayFirstDot, DashboardTimeStyle.H24);
        var utc = new DateTime(2026, 6, 10, 13, 30, 0, DateTimeKind.Utc);
        Assert.Equal("10.06.2026 14:30", state.FormatAbsolute(utc));
    }

    [Fact]
    public void FormatAbsolute_DefaultIsUtcIsoWithSeconds()
    {
        var state = StateWith(null, DashboardDateStyle.Iso, DashboardTimeStyle.H24WithSeconds);
        var utc = new DateTime(2026, 6, 10, 14, 30, 5, DateTimeKind.Utc);
        Assert.Equal("2026-06-10 14:30:05", state.FormatAbsolute(utc));
    }

    [Fact]
    public void ToUtc_NormalTimeRoundTrips()
    {
        var state = StateWith("Europe/London", DashboardDateStyle.Iso, DashboardTimeStyle.H24);
        var utc = state.ToUtc(new DateTime(2026, 6, 10, 14, 30, 0), DstResolution.Earliest);
        Assert.Equal(new DateTime(2026, 6, 10, 13, 30, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void ToUtc_AmbiguousFallBackResolvesPerSide()
    {
        var state = StateWith("Europe/London", DashboardDateStyle.Iso, DashboardTimeStyle.H24);
        var wall = new DateTime(2026, 10, 25, 1, 30, 0);
        var earliest = state.ToUtc(wall, DstResolution.Earliest);
        var latest = state.ToUtc(wall, DstResolution.Latest);
        Assert.True(earliest < latest);
        Assert.Equal(TimeSpan.FromHours(1), latest - earliest);
    }

    [Fact]
    public void ToUtc_InvalidSpringForwardSnapsLowerForwardAndUpperBackward()
    {
        var state = StateWith("Europe/London", DashboardDateStyle.Iso, DashboardTimeStyle.H24);
        var wall = new DateTime(2026, 3, 29, 1, 30, 0);
        Assert.Equal(new DateTime(2026, 3, 29, 1, 0, 0, DateTimeKind.Utc), state.ToUtc(wall, DstResolution.Earliest));
        Assert.Equal(new DateTime(2026, 3, 29, 0, 59, 0, DateTimeKind.Utc), state.ToUtc(wall, DstResolution.Latest));
    }

    [Fact]
    public void ToUtc_LowerBoundInMidnightGapSnapsForwardNotIntoPreviousDay()
    {
        // Havana's 2025 DST gap starts at midnight; the lower bound must stay on Mar 9.
        var state = StateWith("America/Havana", DashboardDateStyle.Iso, DashboardTimeStyle.H24);
        var from = state.ToUtc(new DateTime(2025, 3, 9, 0, 0, 0), DstResolution.Earliest);
        Assert.Equal(new DateTime(2025, 3, 9, 5, 0, 0, DateTimeKind.Utc), from);
        Assert.True(from.Day == 9, "lower bound must not fall back into the previous local day");
    }

    [Fact]
    public void Persistence_ParsesValidBlob()
    {
        var json = """{"v":1,"tz":"Europe/London","date":"DayFirstDot","time":"H12"}""";
        Assert.True(DateTimeDisplayState.TryParse(json, out var prefs));
        Assert.Equal("Europe/London", prefs.Tz);
        Assert.Equal("DayFirstDot", prefs.Date);
        Assert.Equal("H12", prefs.Time);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"v":99,"tz":"Europe/London"}""")]
    public void Persistence_RejectsBadBlob(string? json)
    {
        Assert.False(DateTimeDisplayState.TryParse(json, out _));
    }
}
