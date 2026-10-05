using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client.Localization;

namespace Jobby.Tests.Dashboard.Globalization;

public class DashboardLanguagesTests
{
    [Theory]
    [InlineData(DashboardLanguage.English, "en")]
    [InlineData(DashboardLanguage.Russian, "ru")]
    [InlineData(DashboardLanguage.PortugueseBrazil, "pt-BR")]
    public void Code_MapsEachLanguage(DashboardLanguage lang, string code)
        => Assert.Equal(code, DashboardLanguages.Code(lang));

    [Theory]
    [InlineData(DashboardLanguage.English, "English")]
    [InlineData(DashboardLanguage.Russian, "Русский")]
    [InlineData(DashboardLanguage.PortugueseBrazil, "Português (Brasil)")]
    public void NativeName_MapsEachLanguage(DashboardLanguage lang, string name)
        => Assert.Equal(name, DashboardLanguages.NativeName(lang));

    [Theory]
    [InlineData(DashboardLanguage.English, "flags/lang-en.svg")]
    [InlineData(DashboardLanguage.PortugueseBrazil, "flags/lang-pt-br.svg")]
    public void FlagAsset_LowercasesTheCode(DashboardLanguage lang, string asset)
        => Assert.Equal(asset, DashboardLanguages.FlagAsset(lang));

    [Theory]
    [InlineData("ru", true, DashboardLanguage.Russian)]
    [InlineData("pt-BR", true, DashboardLanguage.PortugueseBrazil)]
    [InlineData("de", false, DashboardLanguage.English)]
    [InlineData(null, false, DashboardLanguage.English)]
    public void TryParseCode_AcceptsKnownRejectsUnknown(string? code, bool ok, DashboardLanguage expected)
    {
        Assert.Equal(ok, DashboardLanguages.TryParseCode(code, out var lang));
        Assert.Equal(expected, lang);
    }

    [Fact]
    public void All_ListsEveryLanguageOnce()
        => Assert.Equal(Enum.GetValues<DashboardLanguage>().Length, DashboardLanguages.All.Count);
}
