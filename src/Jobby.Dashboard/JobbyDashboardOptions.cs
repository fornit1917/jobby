using Jobby.Core.Models.Dashboard;

namespace Jobby.Dashboard;

public sealed class JobbyDashboardOptions
{
    public TimeSpan? RefreshInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int StaleServerThresholdSeconds { get; set; } = 300;

    public bool ReadOnly { get; set; }

    /// <summary>
    /// Time zone in which timestamps are displayed until the user selects another one in the dashboard.
    /// <c>null</c> (default) displays them in the time zone of the user's browser.
    /// </summary>
    public TimeZoneInfo? DefaultTimeZone { get; set; }

    public DashboardDateStyle DefaultDateStyle { get; set; } = DashboardDateStyle.Iso;

    public DashboardTimeStyle DefaultTimeStyle { get; set; } = DashboardTimeStyle.H24WithSeconds;

    public DashboardLanguage DefaultLanguage { get; set; } = DashboardLanguage.English;
}
