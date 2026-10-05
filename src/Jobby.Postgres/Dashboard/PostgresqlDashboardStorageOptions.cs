namespace Jobby.Postgres.Dashboard;

public sealed class PostgresqlDashboardStorageOptions
{
    public string SchemaName { get; set; } = string.Empty;

    public string TablesPrefix { get; set; } = "jobby_";
}