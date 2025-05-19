using Npgsql;

namespace Jobby.Postgres.ConfigurationExtensions;

public interface IPostgresStorageConfigurable
{
    IPostgresStorageConfigurable UseDataSource(NpgsqlDataSource dataSource);
    IPostgresStorageConfigurable UseSchemaName(string schemaName);
    IPostgresStorageConfigurable UseTablesPrefix(string tablesPrefix);
}
