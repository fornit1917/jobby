namespace Jobby.Core.Models.Dashboard;

public sealed record JobbyDashboardClientConfigDto
{
    public double? RefreshIntervalSeconds { get; init; }

    public int StaleServerThresholdSeconds { get; init; }

    public bool ReadOnly { get; init; }

    public bool ManagementEnabled { get; init; }

    public string? ManagementRequestHeaderName { get; init; }

    public string? ManagementRequestToken { get; init; }

    public string? DefaultTimeZoneId { get; init; }

    public DashboardDateStyle DefaultDateStyle { get; init; } = DashboardDateStyle.Iso;

    // Deliberately not the enum zero value: older callers that omit the field must keep seconds.
    public DashboardTimeStyle DefaultTimeStyle { get; init; } = DashboardTimeStyle.H24WithSeconds;

    // The client uses this to avoid relative-time and staleness drift on skewed clocks.
    public DateTime ServerTimeUtc { get; init; }

    public DashboardLanguage DefaultLanguage { get; init; } = DashboardLanguage.English;
}
