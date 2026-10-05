using Bunit;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client;
using Jobby.Dashboard.Client.Localization;
using Jobby.Dashboard.Client.Shared;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Jobby.Tests.Dashboard.Components;

public class LanguageSelectorTests : MudTestContext
{
    private LanguageState Setup()
    {
        var config = new DashboardClientConfig();
        var state = new LanguageState(config);
        Services.AddSingleton(config);
        Services.AddSingleton(state);
        return state;
    }

    [Fact]
    public void Pill_ShowsActiveFlagAndAriaLabel()
    {
        var state = Setup();
        state.ApplyForTests(DashboardLanguage.Russian);

        var cut = Render<LanguageSelector>();

        var pill = cut.Find("[data-testid=language-pill]");
        Assert.Equal("Язык: Русский", pill.GetAttribute("aria-label"));
        Assert.Contains("flags/lang-ru.svg", cut.Find("[data-testid=language-pill] img").GetAttribute("src"));
    }

    [Fact]
    public void Clicking_ThePillOpensTheLanguageList()
    {
        Setup();
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<LanguageSelector>(1);
            builder.CloseComponent();
        });

        Assert.Empty(cut.FindAll("[data-testid=language-item-ru]"));
        cut.Find("[data-testid=language-pill]").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("[data-testid=language-item-en]"));
            Assert.NotEmpty(cut.FindAll("[data-testid=language-item-ru]"));
            Assert.NotEmpty(cut.FindAll("[data-testid=language-item-pt-BR]"));
        });
    }

    [Fact]
    public void Selecting_ALanguageUpdatesStateAndRaisesChanged()
    {
        var state = Setup();
        var changed = 0;
        state.Changed += () => changed++;

        var cut = Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<LanguageSelector>(1);
            builder.CloseComponent();
        });
        cut.Find("[data-testid=language-pill]").Click();
        cut.WaitForElement("[data-testid=language-item-ru]").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.True(changed > 0);
            Assert.Equal(DashboardLanguage.Russian, state.Language);
        });
    }
}