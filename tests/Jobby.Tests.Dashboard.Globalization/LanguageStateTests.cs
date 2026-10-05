using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client;
using Jobby.Dashboard.Client.Localization;

namespace Jobby.Tests.Dashboard.Globalization;

public class LanguageStateTests
{
    private static LanguageState New() => new(new DashboardClientConfig());

    [Fact]
    public void Default_LanguageIsEnglish()
    {
        var l = New();
        Assert.Equal(DashboardLanguage.English, l.Language);
        Assert.False(l.HasUserOverride);
        Assert.Equal("Jobs", l["nav.jobs"]);
    }

    [Fact]
    public void Override_SwitchesActiveTable()
    {
        var l = New();
        l.ApplyForTests(DashboardLanguage.Russian);
        Assert.True(l.HasUserOverride);
        Assert.Equal("Задачи", l["nav.jobs"]);
    }

    [Fact]
    public void Unknown_KeyReturnsTheKeyItself()
    {
        var l = New();
        Assert.Equal("nope.missing", l["nope.missing"]);
        Assert.False(l.TryGet("nope.missing", out _));
    }

    [Fact]
    public void Missing_InActiveFallsBackToEnglish()
    {
        var tables = new Dictionary<DashboardLanguage, IReadOnlyDictionary<string, string>>
        {
            [DashboardLanguage.English] = new Dictionary<string, string> { ["only.en"] = "EN only", ["shared"] = "EN" },
            [DashboardLanguage.Russian] = new Dictionary<string, string> { ["shared"] = "RU" },
        };
        var l = new LanguageState(new DashboardClientConfig(), tables);
        l.ApplyForTests(DashboardLanguage.Russian);

        Assert.Equal("RU", l["shared"]);
        Assert.Equal("EN only", l["only.en"]);
        Assert.True(l.TryGet("only.en", out _));
        Assert.False(l.TryGet("nope.missing", out _));
    }

    [Fact]
    public void Format_SubstitutesArgsInvariantly()
    {
        var l = New();
        Assert.Equal("Page 3", l.Format("common.pageN", 3));
    }

    [Fact]
    public void ApplyForTests_RejectsUndefinedEnumAsNoOverride()
    {
        var l = New();
        l.ApplyForTests((DashboardLanguage)999);
        Assert.False(l.HasUserOverride);
        Assert.Equal(DashboardLanguage.English, l.Language);
    }

    [Theory]
    [InlineData("{\"v\":1,\"lang\":\"ru\"}", DashboardLanguage.Russian)]
    [InlineData("{\"v\":1,\"lang\":\"pt-BR\"}", DashboardLanguage.PortugueseBrazil)]
    public void ParseOverride_AcceptsValid(string json, DashboardLanguage expected)
        => Assert.Equal(expected, LanguageState.ParseOverride(json));

    [Theory]
    [InlineData("{\"v\":2,\"lang\":\"ru\"}")]
    [InlineData("{\"v\":1,\"lang\":\"de\"}")]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseOverride_RejectsInvalidAsNull(string? json)
        => Assert.Null(LanguageState.ParseOverride(json));
}