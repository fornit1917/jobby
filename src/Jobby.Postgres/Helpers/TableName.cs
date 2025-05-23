namespace Jobby.Postgres.Helpers;

internal class TableName
{
    public static string For(string name, PostgresqlStorageSettings settings)
    {
        return string.IsNullOrEmpty(settings.SchemaName)
            ? $"\"{settings.TablesPrefix}{name}\""
            : $"\"{settings.SchemaName}\".\"{settings.TablesPrefix}{name}\"";
    }

    public static string Jobs(PostgresqlStorageSettings settings)
    {
        return For("jobs", settings);
    }

    public static string Servers(PostgresqlStorageSettings settings)
    {
        return For("servers", settings);
    }
}
