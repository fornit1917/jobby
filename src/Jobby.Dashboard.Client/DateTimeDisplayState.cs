using System.Text.Json;
using Jobby.Core.Models.Dashboard;
using Microsoft.JSInterop;

namespace Jobby.Dashboard.Client;

public enum DstResolution
{
    Earliest,
    Latest
}

public sealed class DateTimeDisplayState
{
    private const string StorageKey = "jobby.dashboard.datetime";
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly DashboardClientConfig _config;

    private string? _overrideTimeZoneId;
    private DashboardDateStyle? _overrideDateStyle;
    private DashboardTimeStyle? _overrideTimeStyle;

    public DateTimeDisplayState(DashboardClientConfig config) => _config = config;

    public event Action? Changed;

    public TimeZoneInfo TimeZone { get; private set; } = TimeZoneInfo.Utc;
    public DashboardDateStyle DateStyle { get; private set; } = DashboardDateStyle.Iso;
    public DashboardTimeStyle TimeStyle { get; private set; } = DashboardTimeStyle.H24WithSeconds;

    public bool HasUserOverrides =>
        _overrideTimeZoneId is not null || _overrideDateStyle is not null || _overrideTimeStyle is not null;

    public string FormatAbsolute(DateTime utc)
    {
        var instant = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(instant, TimeZone);
        return DateTimeFormatPatterns.Format(local, DateStyle, TimeStyle);
    }

    // Preserves date-range bounds across DST folds and gaps.
    public DateTime ToUtc(DateTime wallClock, DstResolution resolution)
    {
        var unspecified = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);

        if (TimeZone.IsAmbiguousTime(unspecified))
        {
            var offsets = TimeZone.GetAmbiguousTimeOffsets(unspecified);
            var chosen = resolution == DstResolution.Earliest ? offsets.Max() : offsets.Min();
            return DateTime.SpecifyKind(unspecified - chosen, DateTimeKind.Utc);
        }

        if (TimeZone.IsInvalidTime(unspecified))
        {
            var step = TimeSpan.FromMinutes(1);
            var probe = unspecified;
            while (TimeZone.IsInvalidTime(probe))
            {
                probe = resolution == DstResolution.Earliest ? probe + step : probe - step;
            }

            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(probe, DateTimeKind.Unspecified), TimeZone);
        }

        return TimeZoneInfo.ConvertTimeToUtc(unspecified, TimeZone);
    }

    public async Task InitializeAsync(IJSRuntime js)
    {
        string? stored = null;
        try
        {
            stored = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        }
        catch
        {
            // ignored
        }

        if (TryParse(stored, out var prefs))
        {
            _overrideTimeZoneId = string.IsNullOrWhiteSpace(prefs.Tz) ? null : prefs.Tz;
            _overrideDateStyle = ParseDateStyle(prefs.Date);
            _overrideTimeStyle = ParseTimeStyle(prefs.Time);
        }

        Recompute();
        Changed?.Invoke();
    }

    public async Task SetAsync(IJSRuntime js, string timeZoneId, DashboardDateStyle date, DashboardTimeStyle time)
    {
        _overrideTimeZoneId = timeZoneId;
        _overrideDateStyle = date;
        _overrideTimeStyle = time;
        Recompute();
        await SaveAsync(js);
        Changed?.Invoke();
    }

    public async Task ResetAsync(IJSRuntime js)
    {
        _overrideTimeZoneId = null;
        _overrideDateStyle = null;
        _overrideTimeStyle = null;
        Recompute();
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        }
        catch
        {
            // ignored
        }

        Changed?.Invoke();
    }

    internal void ApplyForTests(string? timeZoneId, DashboardDateStyle date, DashboardTimeStyle time)
    {
        _overrideTimeZoneId = timeZoneId;
        _overrideDateStyle = date;
        _overrideTimeStyle = time;
        Recompute();
    }

    internal void RaiseChangedForTests() => Changed?.Invoke();

    private async Task SaveAsync(IJSRuntime js)
    {
        var prefs = new StoredPrefs
        {
            V = SchemaVersion,
            Tz = _overrideTimeZoneId,
            Date = _overrideDateStyle?.ToString(),
            Time = _overrideTimeStyle?.ToString(),
        };
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(prefs, JsonOptions));
        }
        catch
        {
            // ignored
        }
    }

    private void Recompute()
    {
        TimeZone = ResolveTimeZone(_overrideTimeZoneId ?? _config.DefaultTimeZoneId);
        DateStyle = _overrideDateStyle ?? _config.DefaultDateStyle;
        TimeStyle = _overrideTimeStyle ?? _config.DefaultTimeStyle;
    }

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Utc;
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz) ? tz : TimeZoneInfo.Utc;
    }

    internal static bool TryParse(string? json, out StoredPrefs prefs)
    {
        prefs = new StoredPrefs();
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<StoredPrefs>(json, JsonOptions);
            if (parsed is null || parsed.V != SchemaVersion)
            {
                return false;
            }

            prefs = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static DashboardDateStyle? ParseDateStyle(string? s)
        => Enum.TryParse<DashboardDateStyle>(s, out var v) && Enum.IsDefined(v) ? v : null;

    private static DashboardTimeStyle? ParseTimeStyle(string? s)
        => Enum.TryParse<DashboardTimeStyle>(s, out var v) && Enum.IsDefined(v) ? v : null;

    internal sealed record StoredPrefs
    {
        public int V { get; init; }
        public string? Tz { get; init; }
        public string? Date { get; init; }
        public string? Time { get; init; }
    }
}