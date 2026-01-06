using System.Transactions;
using EvolveDb;
using EvolveDb.Configuration;
using Jobby.Core.Interfaces;
using Jobby.Postgres.Helpers;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Jobby.Postgres;

internal class PostgresqlJobbyStorageMigrator : IJobbyStorageMigrator
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresqlStorageSettings _settings;
    private readonly ILogger<PostgresqlJobbyStorageMigrator> _logger;

    public PostgresqlJobbyStorageMigrator(NpgsqlDataSource dataSource,
        PostgresqlStorageSettings settings,
        ILogger<PostgresqlJobbyStorageMigrator> logger)
    {
        _dataSource = dataSource;
        _settings = settings;
        _logger = logger;
    }

    public void Migrate()
    {
        var conn = _dataSource.OpenConnection();
        var evolve = new Evolve(conn, msg => _logger.LogInformation(msg))
        {
            MetadataTableSchema = _settings.SchemaName,
            MetadataTableName = "jobby_evolve_migrations",
            Schemas = string.IsNullOrEmpty(_settings.SchemaName) ? [] : [_settings.SchemaName],
            TransactionMode = TransactionKind.CommitAll,
            IsEraseDisabled = true,
            OutOfOrder = true,
            EmbeddedResourceAssemblies = [typeof(PostgresqlJobbyStorageMigrator).Assembly],
            EmbeddedResourceFilters = ["Jobby.Postgres.Migrations"],
            Placeholders = new Dictionary<string, string>()
            {
                ["${tables_prefix}"] = _settings.TablesPrefix,
                ["${jobs_table_fullname}"] = TableName.Jobs(_settings),
                ["${servers_table_fullname}"] = TableName.Servers(_settings)
            },
        };

        var trOpts = new TransactionOptions
        {
            IsolationLevel = IsolationLevel.ReadCommitted
        };
        using var transactionScope = new TransactionScope(
            TransactionScopeOption.Required, 
            trOpts, 
            TransactionScopeAsyncFlowOption.Enabled);

        evolve.Migrate();

        transactionScope.Complete();
    }   
}