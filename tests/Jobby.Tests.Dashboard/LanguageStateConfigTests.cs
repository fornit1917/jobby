using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client;
using Jobby.Dashboard.Client.Localization;
using Jobby.Tests.Dashboard.Components;

namespace Jobby.Tests.Dashboard;

public class LanguageStateConfigTests
{
    [Fact]
    public async Task Server_DefaultAppliesAfterConfigLoadsWithoutAnOverride()
    {
        var config = new DashboardClientConfig();
        var state = new LanguageState(config);
        var raised = 0;
        state.Changed += () => raised++;

        var handler = new RecordingHttpMessageHandler(_ => new JobbyDashboardClientConfigDto
        {
            DefaultLanguage = DashboardLanguage.Russian,
        });
        await config.LoadAsync(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        Assert.Equal(DashboardLanguage.Russian, state.Language);
        Assert.True(raised > 0);
        Assert.False(state.HasUserOverride);
    }
}