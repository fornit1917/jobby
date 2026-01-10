namespace Jobby.Postgres;

internal class PostgresqlStorageSettings
{
    public string SchemaName { get; init; } = string.Empty;
    public string TablesPrefix { get; init; } = "jobby_";
}
