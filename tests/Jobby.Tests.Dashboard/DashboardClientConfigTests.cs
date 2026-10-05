using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client;
using Jobby.Tests.Dashboard.Components;

namespace Jobby.Tests.Dashboard;

public class DashboardClientConfigTests
{
    private static HttpClient HttpReturning(JobbyDashboardClientConfigDto dto)
    {
        var handler = new RecordingHttpMessageHandler(_ => dto);
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
    }

    [Fact]
    public async Task Apply_SurfacesDatetimeDefaults()
    {
        var http = HttpReturning(new JobbyDashboardClientConfigDto
        {
            DefaultTimeZoneId = "Europe/London",
            DefaultDateStyle = DashboardDateStyle.DayFirstDot,
            DefaultTimeStyle = DashboardTimeStyle.H12,
            ServerTimeUtc = DateTime.UtcNow,
        });
        var config = new DashboardClientConfig();
        await config.LoadAsync(http);

        Assert.Equal("Europe/London", config.DefaultTimeZoneId);
        Assert.Equal(DashboardDateStyle.DayFirstDot, config.DefaultDateStyle);
        Assert.Equal(DashboardTimeStyle.H12, config.DefaultTimeStyle);
    }

    [Fact]
    public async Task ServerUtcNow_ReflectsClockOffset()
    {
        var serverTime = DateTime.UtcNow.AddMinutes(10);
        var http = HttpReturning(new JobbyDashboardClientConfigDto { ServerTimeUtc = serverTime });
        var config = new DashboardClientConfig();
        await config.LoadAsync(http);

        var delta = config.ServerUtcNow - DateTime.UtcNow;
        Assert.InRange(delta.TotalSeconds, 590, 610);
    }

    [Fact]
    public void ServerUtcNow_DefaultsToClientClockBeforeLoad()
    {
        var config = new DashboardClientConfig();
        Assert.InRange((config.ServerUtcNow - DateTime.UtcNow).TotalSeconds, -2, 2);
    }

    [Fact]
    public async Task Apply_SurfacesDefaultLanguage()
    {
        var http = HttpReturning(new JobbyDashboardClientConfigDto
        {
            DefaultLanguage = DashboardLanguage.PortugueseBrazil,
        });
        var config = new DashboardClientConfig();
        await config.LoadAsync(http);

        Assert.Equal(DashboardLanguage.PortugueseBrazil, config.DefaultLanguage);
    }

    [Fact]
    public void Dto_OmittingStyleFieldsKeepsBackwardCompatibleDefaults()
    {
        var dto = new JobbyDashboardClientConfigDto();
        Assert.Equal(DashboardDateStyle.Iso, dto.DefaultDateStyle);
        Assert.Equal(DashboardTimeStyle.H24WithSeconds,
            dto.DefaultTimeStyle);
    }
}