using Npgsql;

namespace Jobby.Postgres.ConfigurationExtensions;

public interface IPostgresqlStorageConfigurable
{
    IPostgresqlStorageConfigurable UseDataSource(NpgsqlDataSource dataSource);
    IPostgresqlStorageConfigurable UseSchemaName(string schemaName);
    IPostgresqlStorageConfigurable UseTablesPrefix(string tablesPrefix);
}
