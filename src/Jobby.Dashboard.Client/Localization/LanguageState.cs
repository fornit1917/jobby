using System.Globalization;
using System.Text.Json;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Microsoft.JSInterop;

namespace Jobby.Dashboard.Client.Localization;

public sealed class LanguageState
{
    private const string StorageKey = "jobby.dashboard.language";
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly DashboardClientConfig _config;
    private readonly IReadOnlyDictionary<DashboardLanguage, IReadOnlyDictionary<string, string>> _tables;
    private DashboardLanguage? _override;
    private bool _initialized;

    public LanguageState(DashboardClientConfig config)
        : this(config, LoadTables())
    {
    }

    internal LanguageState(DashboardClientConfig config,
        IReadOnlyDictionary<DashboardLanguage, IReadOnlyDictionary<string, string>> tables)
    {
        _config = config;
        _tables = tables;
        _config.Changed += OnConfigChanged;
    }

    private void OnConfigChanged()
    {
        var previous = Language;
        Recompute();
        if (Language != previous)
        {
            Changed?.Invoke();
        }
    }

    public event Action? Changed;
    public DashboardLanguage Language { get; private set; } = DashboardLanguage.English;
    public bool HasUserOverride => _override is not null;

    internal IReadOnlyDictionary<DashboardLanguage, IReadOnlyDictionary<string, string>> Tables => _tables;

    public string this[string key] => TryGet(key, out var v) ? v : key;

    public bool TryGet(string key, out string value)
    {
        if (_tables.TryGetValue(Language, out var table) && table.TryGetValue(key, out var hit))
        {
            value = hit;
            return true;
        }

        if (Language != DashboardLanguage.English
            && _tables.TryGetValue(DashboardLanguage.English, out var en)
            && en.TryGetValue(key, out var enHit))
        {
            value = enHit;
            return true;
        }

        value = key;
        return false;
    }

    public string Format(string key, params object[] args)
        => string.Format(CultureInfo.InvariantCulture, this[key], args);

    public string Status(JobStatus status) => this[$"status.{status}"];

    public string Role(LockedGroupJobRole role) => this[$"role.{role}"];

    public string Relative(DateTime utc, bool allowFuture = true)
    {
        var deltaSeconds = (int)(_config.ServerUtcNow - utc).TotalSeconds;
        var future = deltaSeconds < 0;
        if (future && !allowFuture)
        {
            deltaSeconds = 0;
            future = false;
        }

        var s = Math.Abs(deltaSeconds);
        if (s < 1)
        {
            return this["relative.justNow"];
        }

        var (count, unitKey) = s switch
        {
            < 60 => (s, "relative.unit.second"),
            < 3600 => (s / 60, "relative.unit.minute"),
            < 86400 => (s / 3600, "relative.unit.hour"),
            _ => (s / 86400, "relative.unit.day"),
        };

        return Format(future ? "relative.future" : "relative.past", count, this[unitKey]);
    }

    public async Task InitializeAsync(IJSRuntime js)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        var previous = Language;
        string? stored = null;
        try
        {
            stored = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        }
        catch
        {
            // ignored
        }

        _override = ParseOverride(stored);
        Recompute();
        await ApplyHtmlLangAsync(js);
        if (Language != previous)
        {
            Changed?.Invoke();
        }
    }

    // Called before Blazor's first render from the value read by index.html.
    public void SeedBootLanguage(string? code)
    {
        if (DashboardLanguages.TryParseCode(code, out var lang))
        {
            _override = lang;
            Recompute();
        }
    }

    public async Task SetAsync(IJSRuntime js, DashboardLanguage lang)
    {
        if (!Enum.IsDefined(lang))
        {
            throw new ArgumentOutOfRangeException(nameof(lang), lang, "Unknown dashboard language.");
        }

        _override = lang;
        Recompute();
        await SaveAsync(js);
        await ApplyHtmlLangAsync(js);
        Changed?.Invoke();
    }

    public async Task ResetAsync(IJSRuntime js)
    {
        _override = null;
        Recompute();
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        }
        catch
        {
            // ignored
        }

        await ApplyHtmlLangAsync(js);
        Changed?.Invoke();
    }

    internal void ApplyForTests(DashboardLanguage? @override)
    {
        _override = @override is { } l && Enum.IsDefined(l) ? l : null;
        Recompute();
    }

    internal void RaiseChangedForTests() => Changed?.Invoke();

    private void Recompute()
    {
        var def = Enum.IsDefined(_config.DefaultLanguage) ? _config.DefaultLanguage : DashboardLanguage.English;
        Language = _override ?? def;
    }

    private async Task ApplyHtmlLangAsync(IJSRuntime js)
    {
        try
        {
            await js.InvokeVoidAsync("document.documentElement.setAttribute", "lang",
                DashboardLanguages.Code(Language));
        }
        catch
        {
            // ignored
        }
    }

    private async Task SaveAsync(IJSRuntime js)
    {
        var prefs = new StoredPrefs { V = SchemaVersion, Lang = DashboardLanguages.Code(Language) };
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(prefs, JsonOptions));
        }
        catch
        {
            // ignored
        }
    }

    internal static DashboardLanguage? ParseOverride(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var parsed = JsonSerializer.Deserialize<StoredPrefs>(json, JsonOptions);
            if (parsed is null || parsed.V != SchemaVersion) return null;
            return DashboardLanguages.TryParseCode(parsed.Lang, out var lang) ? lang : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<DashboardLanguage, IReadOnlyDictionary<string, string>> LoadTables()
    {
        var asm = typeof(LanguageState).Assembly;
        var names = asm.GetManifestResourceNames();
        var result = new Dictionary<DashboardLanguage, IReadOnlyDictionary<string, string>>();
        foreach (var lang in DashboardLanguages.All)
        {
            var suffix = $".{DashboardLanguages.Code(lang)}.json";
            var name = names.FirstOrDefault(n => n.EndsWith(suffix, StringComparison.Ordinal))
                       ?? throw new InvalidOperationException($"Embedded localization resource not found: *{suffix}");
            using var stream = asm.GetManifestResourceStream(name)!;
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                       ?? throw new InvalidOperationException($"Localization resource is empty: {name}");
            result[lang] = dict;
        }

        return result;
    }

    internal sealed record StoredPrefs
    {
        public int V { get; init; }
        public string? Lang { get; init; }
    }
}
