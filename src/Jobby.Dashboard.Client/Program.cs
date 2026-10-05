using Jobby.Dashboard.Client;
using Jobby.Dashboard.Client.Localization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<DashboardApiClient>();
builder.Services.AddSingleton<DashboardClientConfig>();
builder.Services.AddSingleton<DashboardModeState>();
builder.Services.AddSingleton<DateTimeDisplayState>();
builder.Services.AddSingleton<LanguageState>();
builder.Services.AddSingleton<MudLocalizer, JobbyMudLocalizer>();

var host = builder.Build();
await host.RunAsync();