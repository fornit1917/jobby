using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client;
using Jobby.Dashboard.Client.Localization;

namespace Jobby.Tests.Dashboard.Globalization;

public class JobbyMudLocalizerTests
{
    private static JobbyMudLocalizer New(out LanguageState state)
    {
        state = new LanguageState(new DashboardClientConfig());
        return new JobbyMudLocalizer(state);
    }

    [Fact]
    public void Known_MudKeyIsTranslatedAndFound()
    {
        var loc = New(out var state);
        state.ApplyForTests(DashboardLanguage.Russian);
        var s = loc["MudDataGridPager_RowsPerPage"];
        Assert.False(s.ResourceNotFound);
        Assert.Equal("Строк на странице:", s.Value);
    }

    [Fact]
    public void Unknown_MudKeyReportsNotFoundSoMudblazorFallsBack()
    {
        var loc = New(out _);
        var s = loc["SomeKeyWeNeverTranslate"];
        Assert.True(s.ResourceNotFound);
        Assert.Equal("SomeKeyWeNeverTranslate", s.Value);
    }
}
