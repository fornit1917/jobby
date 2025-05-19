namespace Jobby.Postgres.Helpers;

internal class TableName
{
    public static string For(string name, PgStorageSettings settings)
    {
        return string.IsNullOrEmpty(settings.SchemaName)
            ? $"\"{settings.TablesPrefix}{name}\""
            : $"\"{settings.SchemaName}\".\"{settings.TablesPrefix}{name}\"";
    }

    public static string Jobs(PgStorageSettings settings)
    {
        return For("jobs", settings);
    }
}
