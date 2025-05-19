using Jobby.Core.Exceptions;
using Npgsql;

namespace Jobby.Postgres.ConfigurationExtensions;

internal class PgStorageBuilder : IPostgresStorageConfigurable
{
    private NpgsqlDataSource? _dataSource;
    private PgStorageSettings _settings = new PgStorageSettings();

    public IPostgresStorageConfigurable UseDataSource(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
        return this;
    }

    public IPostgresStorageConfigurable UseSchemaName(string schemaName)
    {
        _settings = new PgStorageSettings 
        { 
            SchemaName = schemaName, 
            TablesPrefix = _settings.TablesPrefix 
        };
        return this;
    }

    public IPostgresStorageConfigurable UseTablesPrefix(string tablesPrefix)
    {
        _settings = new PgStorageSettings
        {
            SchemaName = _settings.SchemaName,
            TablesPrefix = tablesPrefix
        };
        return this;
    }

    public PgJobsStorage Build()
    {
        if (_dataSource == null) 
        {
            throw new InvalidBuilderConfigException("DataSource is not configured for PostgresStorage. UseDataSource method should be called");
        }
        return new PgJobsStorage(_dataSource, _settings);
    }
}
