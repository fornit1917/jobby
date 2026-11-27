namespace Jobby.Postgres.Dashboard;

public sealed class PostgresqlDashboardStorageOptions
{
    private TimeSpan _countsCacheDuration = TimeSpan.FromSeconds(2);

    public string SchemaName { get; set; } = string.Empty;

    public string TablesPrefix { get; set; } = "jobby_";

    /// <summary>
    /// How long one application instance reuses the per-queue and per-status job counts shown by the dashboard.
    /// Keep it shorter than the dashboard refresh interval; zero shares the counts only between concurrent requests.
    /// </summary>
    public TimeSpan CountsCacheDuration
    {
        get => _countsCacheDuration;
        set => _countsCacheDuration = value >= TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "The duration cannot be negative.");
    }
}