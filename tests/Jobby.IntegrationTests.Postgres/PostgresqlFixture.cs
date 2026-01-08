using Jobby.Core.Services;
using Jobby.IntegrationTests.Postgres.Helpers;
using Jobby.Postgres.ConfigurationExtensions;

namespace Jobby.IntegrationTests.Postgres;

// ReSharper disable once ClassNeverInstantiated.Global
public class PostgresqlFixture
{
    public PostgresqlFixture()
    {
        var jobbyBuilder = new JobbyBuilder();
        jobbyBuilder.UsePostgresql(DbHelper.DataSource);
        jobbyBuilder.CreateStorageMigrator().Migrate();
    }
}

[CollectionDefinition(PostgresqlTestsCollection.Name)]
public class PostgresqlTestsCollection : IClassFixture<PostgresqlFixture>
{
    public const string Name = "Jobby.IntegrationTests.Postgres";
}