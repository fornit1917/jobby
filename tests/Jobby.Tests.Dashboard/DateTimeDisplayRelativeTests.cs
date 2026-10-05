using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client;
using Jobby.Dashboard.Client.Localization;
using Jobby.Tests.Dashboard.Components;

namespace Jobby.Tests.Dashboard;

public class DateTimeDisplayRelativeTests
{
    [Fact]
    public async Task Relative_UsesServerCorrectedClock()
    {
        var handler = new RecordingHttpMessageHandler(_ => new JobbyDashboardClientConfigDto
        {
            ServerTimeUtc = DateTime.UtcNow.AddMinutes(10).AddSeconds(5),
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var config = new DashboardClientConfig();
        await config.LoadAsync(http);

        var l = new LanguageState(config);
        Assert.Equal("10m ago",
            l.Relative(DateTime.UtcNow, allowFuture: false));
    }
}