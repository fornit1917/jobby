using Bunit;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client;
using Jobby.Dashboard.Client.Shared;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Jobby.Tests.Dashboard.Components;

public class DateTimeSettingsTests : MudTestContext
{
    [Fact]
    public void Pill_ShowsJustUTCForTheDefaultZone()
    {
        var config = new DashboardClientConfig();
        var state = new DateTimeDisplayState(config);
        state.ApplyForTests("UTC", DashboardDateStyle.Iso, DashboardTimeStyle.H24);
        Services.AddSingleton(config);
        Services.AddSingleton(state);

        var cut = Render<DateTimeSettings>();

        var pill = cut.Find("[data-testid=datetime-pill]");
        Assert.Equal("UTC", pill.TextContent.Trim());
    }

    [Fact]
    public void Pill_ShowsTimezoneAndUtcOffsetForANonUtcZone()
    {
        var config = new DashboardClientConfig();
        var state = new DateTimeDisplayState(config);
        state.ApplyForTests("Asia/Kolkata", DashboardDateStyle.Iso, DashboardTimeStyle.H24);
        Services.AddSingleton(config);
        Services.AddSingleton(state);

        var cut = Render<DateTimeSettings>();

        var pill = cut.Find("[data-testid=datetime-pill]");
        Assert.Contains("Asia/Kolkata", pill.TextContent);
        Assert.Contains("UTC+5:30", pill.TextContent);
    }

    [Fact]
    public void Clicking_ThePillOpensTheSettingsPopover()
    {
        var config = new DashboardClientConfig();
        var state = new DateTimeDisplayState(config);
        Services.AddSingleton(config);
        Services.AddSingleton(state);

        var cut = Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<DateTimeSettings>(1);
            builder.CloseComponent();
        });

        Assert.Empty(cut.FindAll("[data-testid=datetime-date]"));

        cut.Find("[data-testid=datetime-pill]").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("[data-testid=datetime-tz]"));
            Assert.NotEmpty(cut.FindAll("[data-testid=datetime-date]"));
            Assert.NotEmpty(cut.FindAll("[data-testid=datetime-time]"));
            Assert.NotEmpty(cut.FindAll("[data-testid=datetime-reset]"));
        });
    }
}