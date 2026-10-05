using System.Text.Json;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client;
using Jobby.Dashboard.Client.Localization;

namespace Jobby.Tests.Dashboard.Globalization;

public partial class LocalizationParityTests
{
    private static IReadOnlyDictionary<DashboardLanguage, IReadOnlyDictionary<string, string>> Tables()
        => new LanguageState(new DashboardClientConfig()).Tables;

    [Fact]
    public void All_LanguagesHaveIdenticalKeySets()
    {
        var tables = Tables();
        var en = tables[DashboardLanguage.English].Keys.ToHashSet();
        foreach (var lang in DashboardLanguages.All.Where(l => l != DashboardLanguage.English))
        {
            var keys = tables[lang].Keys.ToHashSet();
            Assert.True(en.SetEquals(keys),
                $"{DashboardLanguages.Code(lang)} key set differs. Missing: " +
                $"[{string.Join(", ", en.Except(keys))}] Extra: [{string.Join(", ", keys.Except(en))}]");
        }
    }

    [Fact]
    public void No_ValueIsEmpty()
    {
        foreach (var (lang, table) in Tables())
        foreach (var (key, value) in table)
            Assert.False(string.IsNullOrWhiteSpace(value),
                $"Empty value for '{key}' in {DashboardLanguages.Code(lang)}.");
    }

    [Fact]
    public void Placeholder_TokensMatchAcrossLanguages()
    {
        var tables = Tables();
        var en = tables[DashboardLanguage.English];
        foreach (var lang in DashboardLanguages.All.Where(l => l != DashboardLanguage.English))
        foreach (var (key, enValue) in en)
        {
            var a = Placeholders(enValue);
            var b = Placeholders(tables[lang][key]);
            Assert.True(a.SetEquals(b),
                $"Placeholder mismatch for '{key}' in {DashboardLanguages.Code(lang)}: en[{string.Join(",", a)}] vs [{string.Join(",", b)}].");
        }

        return;

        static HashSet<string> Placeholders(string s)
            =>
            [
                .. MyRegex().Matches(s)
                    .Select(m => m.Value)
            ];
    }

    [Theory]
    [InlineData("en.json")]
    [InlineData("ru.json")]
    [InlineData("pt-BR.json")]
    public void No_DuplicateKeys(string file)
    {
        var asm = typeof(LanguageState).Assembly;
        var name = asm.GetManifestResourceNames().Single(n => n.EndsWith("." + file, StringComparison.Ordinal));
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var seen = new HashSet<string>();
        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
            Assert.True(seen.Add(prop.Name), $"Duplicate key '{prop.Name}' in {file}.");
    }

    [Fact]
    public void All_ThreeTablesLoadAndAreNonEmpty()
    {
        var tables = Tables();
        Assert.Equal(DashboardLanguages.All.Count, tables.Count);
        foreach (var (_, table) in tables) Assert.NotEmpty(table);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\{(\d+)\}")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}