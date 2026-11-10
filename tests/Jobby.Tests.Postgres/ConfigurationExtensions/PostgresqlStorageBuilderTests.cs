using Jobby.Core.Exceptions;
using Jobby.Postgres;
using Jobby.Postgres.ConfigurationExtensions;
using Npgsql;

namespace Jobby.Tests.Postgres.ConfigurationExtensions;

public class PostgresqlStorageBuilderTests
{
    [Fact]
    public void BuilderMethods_ReturnSameBuilderInstance()
    {
        var builder = new PostgresqlStorageBuilder();

        Assert.Same(builder, builder.UseDataSource(new NpgsqlDataSourceBuilder("Host=localhost").Build()));
        Assert.Same(builder, builder.UseSchemaName("jobby"));
        Assert.Same(builder, builder.UseTablesPrefix("prefix_"));
    }

    [Fact]
    public void BuildStorage_WithoutDataSource_ThrowsInvalidBuilderConfigException()
    {
        var builder = new PostgresqlStorageBuilder();

        var exception = Assert.Throws<InvalidBuilderConfigException>(() => builder.BuildStorage());
        Assert.Contains("DataSource is not configured", exception.Message);
    }

    [Fact]
    public void BuildPermanentLocksStorage_WithoutDataSource_ThrowsInvalidBuilderConfigException()
    {
        var builder = new PostgresqlStorageBuilder();

        var exception = Assert.Throws<InvalidBuilderConfigException>(() => builder.BuildPermanentLocksStorage());
        Assert.Contains("DataSource is not configured", exception.Message);
    }

    [Fact]
    public void BuildMigrator_WithoutDataSource_ThrowsInvalidBuilderConfigException()
    {
        var builder = new PostgresqlStorageBuilder();

        var exception = Assert.Throws<InvalidBuilderConfigException>(() => builder.BuildMigrator(null!));
        Assert.Contains("DataSource is not configured", exception.Message);
    }

    [Fact]
    public void BuildStorage_WithDataSource_CreatesStorage()
    {
        var dataSource = new NpgsqlDataSourceBuilder("Host=localhost").Build();
        var builder = new PostgresqlStorageBuilder();
        builder.UseDataSource(dataSource);

        var storage = builder.BuildStorage();

        Assert.NotNull(storage);
        Assert.IsType<PostgresqlJobbyStorage>(storage);
    }

    [Fact]
    public void BuildPermanentLocksStorage_WithDataSource_CreatesStorage()
    {
        var dataSource = new NpgsqlDataSourceBuilder("Host=localhost").Build();
        var builder = new PostgresqlStorageBuilder();
        builder.UseDataSource(dataSource);

        var storage = builder.BuildPermanentLocksStorage();

        Assert.NotNull(storage);
        Assert.IsType<PostgresqlPermanentLocksStorage>(storage);
    }
}
