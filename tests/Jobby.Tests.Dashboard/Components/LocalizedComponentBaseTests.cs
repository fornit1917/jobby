using Bunit;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client;
using Jobby.Dashboard.Client.Localization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Jobby.Tests.Dashboard.Components;

public class LocalizedComponentBaseTests : MudTestContext
{
    private sealed class Probe : LocalizedComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => builder.AddContent(0, L["nav.jobs"]);
    }

    [Fact]
    public void Rerenders_WhenLanguageChanges()
    {
        var config = new DashboardClientConfig();
        var state = new LanguageState(config);
        Services.AddSingleton(config);
        Services.AddSingleton(state);

        var cut = Render<Probe>();
        cut.MarkupMatches("Jobs");

        state.ApplyForTests(DashboardLanguage.Russian);
        state.RaiseChangedForTests();

        cut.WaitForAssertion(() => cut.MarkupMatches("Задачи"));
    }

    [Fact]
    public void Unsubscribes_OnDispose()
    {
        var config = new DashboardClientConfig();
        var state = new LanguageState(config);
        Services.AddSingleton(config);
        Services.AddSingleton(state);

        var cut = Render<Probe>();
        cut.Instance.Dispose();
        state.RaiseChangedForTests();
    }
}