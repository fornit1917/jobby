using Jobby.Dashboard.Client;
using Jobby.Dashboard.Client.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace Jobby.Tests.Dashboard.Components;

public abstract class DashboardClientTestContext : MudTestContext
{
    protected RecordingHttpMessageHandler Handler { get; private set; } = null!;

    protected void SetupApi(Func<HttpRequestMessage, object?> router)
    {
        Handler = new RecordingHttpMessageHandler(router);
        Services.AddSingleton(new HttpClient(Handler) { BaseAddress = new Uri("http://localhost/") });
        Services.AddSingleton(new DashboardClientConfig());
        Services.AddSingleton<DateTimeDisplayState>();
        Services.AddSingleton<LanguageState>();
        Services.AddScoped<DashboardApiClient>();
    }

    protected async Task<DashboardClientConfig> SetupApiAndLoadConfigAsync(Func<HttpRequestMessage, object?> router)
    {
        Handler = new RecordingHttpMessageHandler(router);
        var http = new HttpClient(Handler) { BaseAddress = new Uri("http://localhost/") };
        var config = new DashboardClientConfig();
        await config.LoadAsync(http);
        Services.AddSingleton(http);
        Services.AddSingleton(config);
        Services.AddSingleton<DateTimeDisplayState>();
        Services.AddSingleton<LanguageState>();
        Services.AddScoped<DashboardApiClient>();
        return config;
    }
}