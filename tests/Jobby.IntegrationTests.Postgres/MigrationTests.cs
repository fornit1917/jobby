using Jobby.IntegrationTests.Postgres.Helpers;
using Jobby.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobby.IntegrationTests.Postgres;

[Collection(PostgresqlTestsCollection.Name)]
public class MigrationTests
{
    // Notes:
    // - SqlQueryRaw is required: SqlQuery turns interpolated holes into query parameters,
    //   and a parameter inside a SQL string literal ('...') stays literal text.
    // - The migrator records applied scripts in the Evolve metadata table "jobby_evolve_migrations"
    //   without a prefix, so a prefix-only migration against the shared public metadata
    //   (already populated by the test fixture) is a no-op. Fresh metadata is guaranteed
    //   by migrating into a dedicated schema.

    [Fact]
    public void Migrate_TwiceWithSameSettings_DoesNotThrow()
    {
        var migrator = new PostgresqlJobbyStorageMigrator(
            DbHelper.DataSource,
            new PostgresqlStorageSettings(),
            NullLogger<PostgresqlJobbyStorageMigrator>.Instance);

        migrator.Migrate();
        migrator.Migrate();
    }

    [Fact]
    public async Task Migrate_WithSchemaAndPrefix_CreatesTablesAndFunctionInSchema()
    {
        const string schema = "mig_test_schema";
        try
        {
            var migrator = new PostgresqlJobbyStorageMigrator(
                DbHelper.DataSource,
                new PostgresqlStorageSettings { SchemaName = schema, TablesPrefix = "migp_" },
                NullLogger<PostgresqlJobbyStorageMigrator>.Instance);

            await using var dbContext = DbHelper.CreateContext();

            migrator.Migrate();

            foreach (var table in new[] { "migp_jobs", "migp_servers", "migp_unlocking_groups" })
            {
                var exists = await TableExistsAsync(dbContext, schema, table);
                Assert.True(exists, $"Table '\"{schema}\".\"{table}\" was not created");
            }

            // the take_to_processing function exists in the same schema, placeholders substituted
            var functionExists = await FunctionExistsAsync(dbContext, schema, "migp_take_to_processing");
            Assert.True(functionExists, $"Function '{schema}.migp_take_to_processing' was not created");
        }
        finally
        {
            // DROP SCHEMA CASCADE removes the tables, the take_to_processing function
            // (PostgreSQL does not track function-to-table dependencies) and the Evolve
            // metadata table of this schema
            await using var dbContext = DbHelper.CreateContext();
            await dbContext.Database.ExecuteSqlRawAsync($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE",
                TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Migrate_WithSchemaName_CreatesTablesInSchema()
    {
        const string schema = "mig_schema_only";
        try
        {
            var migrator = new PostgresqlJobbyStorageMigrator(
                DbHelper.DataSource,
                new PostgresqlStorageSettings { SchemaName = schema },
                NullLogger<PostgresqlJobbyStorageMigrator>.Instance);

            await using var dbContext = DbHelper.CreateContext();

            migrator.Migrate();

            var jobsTableExists = await TableExistsAsync(dbContext, schema, "jobby_jobs");
            Assert.True(jobsTableExists, $"Table '\"{schema}\".jobby_jobs' was not created");
        }
        finally
        {
            await using var dbContext = DbHelper.CreateContext();
            await dbContext.Database.ExecuteSqlRawAsync($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE",
                TestContext.Current.CancellationToken);
        }
    }

    private static async Task<bool> TableExistsAsync(DbContext dbContext, string schema, string table)
    {
        return await dbContext.Database
#pragma warning disable EF1002
            .SqlQueryRaw<bool>($"SELECT to_regclass('\"{schema}\".\"{table}\"') IS NOT NULL AS \"Value\"")
#pragma warning restore EF1002
            .SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
    }

    private static async Task<bool> FunctionExistsAsync(DbContext dbContext, string schema, string functionName)
    {
        return await dbContext.Database
#pragma warning disable EF1002
            .SqlQueryRaw<bool>($"""
                                SELECT EXISTS (
                                    SELECT 1 FROM pg_proc p
                                    JOIN pg_namespace n ON n.oid = p.pronamespace
                                    WHERE p.proname = '{functionName}' AND n.nspname = '{schema}'
                                ) AS "Value"
                                """)
#pragma warning restore EF1002
            .SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
    }
}
