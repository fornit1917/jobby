using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces.Configuration;
using Microsoft.Extensions.Logging;
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
            TablesPrefix = _settings.TablesPrefix,
            SequenceFailureBehavior = _settings.SequenceFailureBehavior
        };
        return this;
    }

    public IPostgresqlStorageConfigurable UseTablesPrefix(string tablesPrefix)
    {
        _settings = new PostgresqlStorageSettings
        {
            SchemaName = _settings.SchemaName,
            TablesPrefix = tablesPrefix,
            SequenceFailureBehavior = _settings.SequenceFailureBehavior
        };
        return this;
    }

    public IPostgresqlStorageConfigurable UseSequenceFailureBehavior(SequenceFailureBehavior behavior)
    {
        _settings = new PostgresqlStorageSettings
        {
            SchemaName = _settings.SchemaName,
            TablesPrefix = _settings.TablesPrefix,
            SequenceFailureBehavior = behavior
        };
        return this;
    }

    internal PostgresqlJobbyStorage BuildStorage()
    {
        if (_dataSource == null) 
        {
            throw new InvalidBuilderConfigException("DataSource is not configured for PostgresStorage. UseDataSource method should be called");
        }
        return new PostgresqlJobbyStorage(_dataSource, _settings);
    }

    internal PostgresqlJobbyStorageMigrator BuildMigrator(ICommonInfrastructure commonInfra)
    {
        if (_dataSource == null) 
        {
            throw new InvalidBuilderConfigException("DataSource is not configured for PostgresStorage. UseDataSource method should be called");
        }
        
        return new PostgresqlJobbyStorageMigrator(_dataSource, _settings,
            commonInfra.LoggerFactory.CreateLogger<PostgresqlJobbyStorageMigrator>());
    }
}
