using Bunit;
using Jobby.Dashboard.Client;
using Jobby.Dashboard.Client.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Jobby.Tests.Dashboard.Components;

public abstract class MudTestContext : BunitContext
{
    protected MudTestContext()
    {
        Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new DashboardModeState());
        Services.AddSingleton(new DashboardClientConfig());
        Services.AddSingleton<LanguageState>();
    }
}