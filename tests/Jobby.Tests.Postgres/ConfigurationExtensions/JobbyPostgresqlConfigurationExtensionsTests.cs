using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Services;
using Jobby.Postgres;
using Jobby.Postgres.ConfigurationExtensions;
using Npgsql;

namespace Jobby.Tests.Postgres.ConfigurationExtensions;

public class JobbyPostgresqlConfigurationExtensionsTests
{
    [Fact]
    public void UsePostgresql_WithConfigureAction_RegistersStorageLocksStorageAndMigrator()
    {
        var dataSource = new NpgsqlDataSourceBuilder("Host=localhost").Build();
        var builder = new JobbyBuilder();

        builder.UsePostgresql(cfg => cfg.UseDataSource(dataSource));

        var migrator = builder.CreateStorageMigrator();
        Assert.IsType<PostgresqlJobbyStorageMigrator>(migrator);

        // storage is created lazily by the builder and used by the client
        var client = builder.CreateJobbyClient();
        Assert.NotNull(client);

        // CreateJobbyServer requires storage, permanent locks storage and scope factory —
        // succeeding proves UsePostgresql registered both storages
        builder.UseExecutionScopeFactory(new EmptyJobbyExecutionScopeFactory());
        Assert.NotNull(builder.CreateJobbyServer());
    }

    [Fact]
    public void UsePostgresql_WithDataSource_RegistersStorageLocksStorageAndMigrator()
    {
        var dataSource = new NpgsqlDataSourceBuilder("Host=localhost").Build();
        var builder = new JobbyBuilder();

        builder.UsePostgresql(dataSource);

        var migrator = builder.CreateStorageMigrator();
        Assert.IsType<PostgresqlJobbyStorageMigrator>(migrator);

        var client = builder.CreateJobbyClient();
        Assert.NotNull(client);

        builder.UseExecutionScopeFactory(new EmptyJobbyExecutionScopeFactory());
        Assert.NotNull(builder.CreateJobbyServer());
    }

    // Note: whether UseSchemaName/UseTablesPrefix are actually applied can only be
    // verified against a real database — see MigrationTests in Jobby.IntegrationTests.Postgres

    [Fact]
    public void UsePostgresql_WithoutDataSource_ThrowsInvalidBuilderConfigException()
    {
        var builder = new JobbyBuilder();

        // storage is built eagerly inside UsePostgresql
        Assert.Throws<InvalidBuilderConfigException>(() => builder.UsePostgresql(cfg => { }));
    }

    private sealed class EmptyJobbyExecutionScope : IJobExecutionScope
    {
        public void Dispose()
        {
        }

        public object? GetService(Type type) => null;
    }

    private sealed class EmptyJobbyExecutionScopeFactory : IJobExecutionScopeFactory
    {
        public IJobExecutionScope CreateJobExecutionScope() => new EmptyJobbyExecutionScope();
    }
}
