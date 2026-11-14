using Jobby.Core.Models.Dashboard;

namespace Jobby.Dashboard;

public sealed class JobbyDashboardOptions
{
    private TimeZoneInfo _defaultTimeZone = TimeZoneInfo.Utc;

    public TimeSpan? RefreshInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int StaleServerThresholdSeconds { get; set; } = 300;

    public bool ReadOnly { get; set; }

    public TimeZoneInfo DefaultTimeZone
    {
        get => _defaultTimeZone;
        set => _defaultTimeZone = value ?? throw new ArgumentNullException(nameof(value));
    }

    public DashboardDateStyle DefaultDateStyle { get; set; } = DashboardDateStyle.Iso;

    public DashboardTimeStyle DefaultTimeStyle { get; set; } = DashboardTimeStyle.H24WithSeconds;

    public DashboardLanguage DefaultLanguage { get; set; } = DashboardLanguage.English;
}
