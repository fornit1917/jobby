using Microsoft.Extensions.Localization;
using MudBlazor;

namespace Jobby.Dashboard.Client.Localization;

public sealed class JobbyMudLocalizer : MudLocalizer
{
    private readonly LanguageState _l;

    public JobbyMudLocalizer(LanguageState l) => _l = l;

    public override LocalizedString this[string key] =>
        _l.TryGet($"mud.{key}", out var value)
            ? new LocalizedString(key, value, resourceNotFound: false)
            : new LocalizedString(key, key, resourceNotFound: true);
}