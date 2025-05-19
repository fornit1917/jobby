using Jobby.Core.Exceptions;
using Npgsql;

namespace Jobby.Postgres.ConfigurationExtensions;

internal class PostgresqlStorageBuilder : IPostgresqlStorageConfigurable
{
    private NpgsqlDataSource? _dataSource;
    private PostgresqlStorageSettings _settings = new PostgresqlStorageSettings();

    public IPostgresqlStorageConfigurable UseDataSource(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
        return this;
    }

    public IPostgresqlStorageConfigurable UseSchemaName(string schemaName)
    {
        _settings = new PostgresqlStorageSettings 
        { 
            SchemaName = schemaName, 
            TablesPrefix = _settings.TablesPrefix 
        };
        return this;
    }

    public IPostgresqlStorageConfigurable UseTablesPrefix(string tablesPrefix)
    {
        _settings = new PostgresqlStorageSettings
        {
            SchemaName = _settings.SchemaName,
            TablesPrefix = tablesPrefix
        };
        return this;
    }

    public PostgresqlJobbyStorage Build()
    {
        if (_dataSource == null) 
        {
            throw new InvalidBuilderConfigException("DataSource is not configured for PostgresStorage. UseDataSource method should be called");
        }
        return new PostgresqlJobbyStorage(_dataSource, _settings);
    }
}
