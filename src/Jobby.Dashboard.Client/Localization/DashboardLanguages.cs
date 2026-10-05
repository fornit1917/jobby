using Jobby.Core.Models.Dashboard;

namespace Jobby.Dashboard.Client.Localization;

public static class DashboardLanguages
{
    public static IReadOnlyList<DashboardLanguage> All { get; } =
    [
        DashboardLanguage.English, DashboardLanguage.Russian, DashboardLanguage.PortugueseBrazil
    ];

    public static string Code(DashboardLanguage lang) => lang switch
    {
        DashboardLanguage.Russian => "ru",
        DashboardLanguage.PortugueseBrazil => "pt-BR",
        _ => "en",
    };

    public static string NativeName(DashboardLanguage lang) => lang switch
    {
        DashboardLanguage.Russian => "Русский",
        DashboardLanguage.PortugueseBrazil => "Português (Brasil)",
        _ => "English",
    };

    public static string FlagAsset(DashboardLanguage lang) => $"flags/lang-{Code(lang).ToLowerInvariant()}.svg";

    public static bool TryParseCode(string? code, out DashboardLanguage lang)
    {
        switch (code)
        {
            case "en":
                lang = DashboardLanguage.English;
                return true;
            case "ru":
                lang = DashboardLanguage.Russian;
                return true;
            case "pt-BR":
                lang = DashboardLanguage.PortugueseBrazil;
                return true;
            default:
                lang = DashboardLanguage.English;
                return false;
        }
    }
}
