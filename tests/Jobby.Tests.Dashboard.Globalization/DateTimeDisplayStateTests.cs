using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.UI;

namespace Jobby.Tests.Dashboard.Globalization;

public class DateTimeDisplayStateTests
{
    // Zones built with explicit rules run on every OS, including Windows under InvariantGlobalization,
    // where the OS database cannot resolve IANA ids (that mapping lives in the disabled ICU).
    private static DateTimeDisplayState StateWith(TimeZoneInfo zone, DashboardDateStyle date,
        DashboardTimeStyle time)
    {
        var state = new DateTimeDisplayState(new DashboardClientConfig());
        state.ApplyForTests(zone, date, time);
        return state;
    }

    [Fact]
    public void FormatAbsolute_ConvertsToTimezoneAndUsesPreset()
    {
        var state = StateWith(TestZones.FixedUtcPlusOne, DashboardDateStyle.DayFirstDot, DashboardTimeStyle.H24);
        var utc = new DateTime(2026, 6, 10, 13, 30, 0, DateTimeKind.Utc);
        Assert.Equal("10.06.2026 14:30 UTC+1", state.FormatAbsolute(utc));
    }

    [Fact]
    public void FormatAbsolute_DefaultIsUtcIsoWithSeconds()
    {
        var state = new DateTimeDisplayState(new DashboardClientConfig());
        state.ApplyForTests(TimeZoneInfo.Utc, DashboardDateStyle.Iso, DashboardTimeStyle.H24WithSeconds);
        var utc = new DateTime(2026, 6, 10, 14, 30, 5, DateTimeKind.Utc);
        Assert.Equal("2026-06-10 14:30:05 UTC", state.FormatAbsolute(utc));
    }

    [Fact]
    public void FormatAbsolute_SuffixIsTheOffsetOfTheFormattedInstant()
    {
        var state = StateWith(TestZones.WinterSummerUtcPlusOne, DashboardDateStyle.Iso, DashboardTimeStyle.H24);

        Assert.Equal("2026-01-10 13:30 UTC",
            state.FormatAbsolute(new DateTime(2026, 1, 10, 13, 30, 0, DateTimeKind.Utc)));
        Assert.Equal("2026-07-10 14:30 UTC+1",
            state.FormatAbsolute(new DateTime(2026, 7, 10, 13, 30, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task InitializeAsync_WithoutOverride_UsesBrowserTimeZone()
    {
        var state = new DateTimeDisplayState(new DashboardClientConfig());

        await state.InitializeAsync(new StubJsRuntime { ["jobbyDashboard.getTimeZone"] = TestZones.TokyoId });

        Assert.Equal(TestZones.TokyoId, state.TimeZone.Id);
        Assert.Equal(TestZones.TokyoId, state.BrowserTimeZoneId);
        Assert.False(state.HasUserOverrides);
    }

    [Fact]
    public async Task InitializeAsync_StoredOverrideWinsOverBrowserTimeZone()
    {
        var state = new DateTimeDisplayState(new DashboardClientConfig());

        await state.InitializeAsync(new StubJsRuntime
        {
            ["jobbyDashboard.getTimeZone"] = TestZones.TokyoId,
            ["localStorage.getItem"] = $$"""{"v":1,"tz":"{{TestZones.LondonId}}","date":"Iso","time":"H24"}""",
        });

        Assert.Equal(TestZones.LondonId, state.TimeZone.Id);
        Assert.True(state.HasUserOverrides);
    }

    [Fact]
    public async Task InitializeAsync_StoredZoneThatCannotBeResolved_FallsBackToBrowserTimeZone()
    {
        var state = new DateTimeDisplayState(new DashboardClientConfig());

        await state.InitializeAsync(new StubJsRuntime
        {
            ["jobbyDashboard.getTimeZone"] = TestZones.TokyoId,
            ["localStorage.getItem"] = """{"v":1,"tz":"Not/A_Zone"}""",
        });

        Assert.Equal(TestZones.TokyoId, state.TimeZone.Id);
    }

    [Fact]
    public async Task SetDateStyleAsync_StoresOnlyTheDateStyle_SoTheTimeZoneKeepsFollowingTheBrowser()
    {
        var js = new StubJsRuntime { ["jobbyDashboard.getTimeZone"] = TestZones.TokyoId };
        var state = new DateTimeDisplayState(new DashboardClientConfig());
        await state.InitializeAsync(js);

        await state.SetDateStyleAsync(js, DashboardDateStyle.DayFirstDot);

        Assert.Equal(TestZones.TokyoId, state.TimeZone.Id);
        var stored = (string)js.Calls.Single(call => call.Identifier == "localStorage.setItem").Args![1]!;
        // The same browser profile opened later in another time zone.
        var later = new DateTimeDisplayState(new DashboardClientConfig());
        await later.InitializeAsync(new StubJsRuntime
        {
            ["jobbyDashboard.getTimeZone"] = TestZones.LondonId,
            ["localStorage.getItem"] = stored,
        });
        Assert.Equal(TestZones.LondonId, later.TimeZone.Id);
        Assert.Equal(DashboardDateStyle.DayFirstDot, later.DateStyle);
    }

    [Fact]
    public async Task SetTimeZoneAsync_KeepsTheChosenZoneWhateverTheBrowserReports()
    {
        var js = new StubJsRuntime { ["jobbyDashboard.getTimeZone"] = TestZones.TokyoId };
        var state = new DateTimeDisplayState(new DashboardClientConfig());
        await state.InitializeAsync(js);

        await state.SetTimeZoneAsync(js, TestZones.LondonId);

        Assert.Equal(TestZones.LondonId, state.TimeZone.Id);
        Assert.True(state.HasUserOverrides);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Not/A_Zone")]
    public async Task InitializeAsync_UnusableBrowserTimeZone_FallsBackToUtc(string? browserTimeZone)
    {
        var state = new DateTimeDisplayState(new DashboardClientConfig());

        await state.InitializeAsync(new StubJsRuntime { ["jobbyDashboard.getTimeZone"] = browserTimeZone });

        Assert.Equal(TimeZoneInfo.Utc, state.TimeZone);
    }

    [Fact]
    public void ToUtc_NormalTimeRoundTrips()
    {
        var state = StateWith(TestZones.FixedUtcPlusOne, DashboardDateStyle.Iso, DashboardTimeStyle.H24);
        var utc = state.ToUtc(new DateTime(2026, 6, 10, 14, 30, 0), DstResolution.Earliest);
        Assert.Equal(new DateTime(2026, 6, 10, 13, 30, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void ToUtc_AmbiguousFallBackResolvesPerSide()
    {
        var state = StateWith(TestZones.FallFoldZone, DashboardDateStyle.Iso, DashboardTimeStyle.H24);
        var wall = new DateTime(2026, 10, 25, 1, 30, 0);
        var earliest = state.ToUtc(wall, DstResolution.Earliest);
        var latest = state.ToUtc(wall, DstResolution.Latest);
        Assert.True(earliest < latest);
        Assert.Equal(TimeSpan.FromHours(1), latest - earliest);
    }

    [Fact]
    public void ToUtc_InvalidSpringForwardSnapsLowerForwardAndUpperBackward()
    {
        // The rule starts on March 29 at 02:00, so the wall times 02:00-03:00 do not exist: the lower
        // bound snaps forward to 03:00 (+1), the upper bound backward to 01:59 (+0).
        var state = StateWith(TestZones.SpringGapZone, DashboardDateStyle.Iso, DashboardTimeStyle.H24);
        var wall = new DateTime(2026, 3, 29, 2, 30, 0);
        Assert.Equal(new DateTime(2026, 3, 29, 2, 0, 0, DateTimeKind.Utc), state.ToUtc(wall, DstResolution.Earliest));
        Assert.Equal(new DateTime(2026, 3, 29, 1, 59, 0, DateTimeKind.Utc), state.ToUtc(wall, DstResolution.Latest));
    }

    [Fact]
    public void ToUtc_LowerBoundInMidnightGapSnapsForwardNotIntoPreviousDay()
    {
        // The DST gap starts at midnight on March 1; the lower bound must stay on March 1.
        var state = StateWith(TestZones.MidnightGapZone, DashboardDateStyle.Iso, DashboardTimeStyle.H24);
        var from = state.ToUtc(new DateTime(2025, 3, 1, 0, 0, 0), DstResolution.Earliest);
        Assert.Equal(new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), from);
        Assert.True(from.Day == 1, "lower bound must not fall back into the previous local day");
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
