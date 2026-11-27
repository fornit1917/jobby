using Bunit;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.UI;
using Jobby.Tests.Dashboard.Components;

namespace Jobby.Tests.Dashboard;

public class DateTimeDisplayStateConfigTests
{
    // The state of a dashboard whose server configured the given default zone, opened in a Tokyo browser.
    private static async Task<DateTimeDisplayState> InitializeInTokyoAsync(string configuredTimeZoneId)
    {
        var handler = new RecordingHttpMessageHandler(_ => new JobbyDashboardClientConfigDto
        {
            DefaultTimeZoneId = configuredTimeZoneId,
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var config = new DashboardClientConfig();
        await config.LoadAsync(http, Xunit.TestContext.Current.CancellationToken);
        var state = new DateTimeDisplayState(config);
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.JSInterop.Setup<string?>("jobbyDashboard.getTimeZone").SetResult("Asia/Tokyo");

        await state.InitializeAsync(context.JSInterop.JSRuntime);
        return state;
    }

    [Fact]
    public async Task ConfiguredDefaultTimeZone_WinsOverBrowserTimeZone()
    {
        var state = await InitializeInTokyoAsync("Europe/London");

        Assert.Equal("Europe/London", state.TimeZone.Id);
        Assert.False(state.HasUserOverrides);
    }

    [Fact]
    public async Task ConfiguredDefaultTimeZoneThatCannotBeResolved_FallsBackToBrowserTimeZone()
    {
        var state = await InitializeInTokyoAsync("Not/A_Zone");

        Assert.Equal("Asia/Tokyo", state.TimeZone.Id);
    }
}
