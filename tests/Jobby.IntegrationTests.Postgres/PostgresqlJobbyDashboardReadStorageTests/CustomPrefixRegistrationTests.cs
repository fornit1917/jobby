using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Npgsql;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyDashboardReadStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class CustomPrefixRegistrationTests
{
    private const string Prefix = "dash_custom_";

    [Fact]
    public async Task DashboardReadStorage_HonoursCustomTablesPrefix()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var _ = await DbHelper.CreateContextAndClearDbAsync())
        {
        }

        await RecreateCustomJobsTableAsync(cancellationToken);
        await using (var conn = await DbHelper.DataSource.OpenConnectionAsync(cancellationToken))
        await using (var insert = new NpgsqlCommand(
                         $@"INSERT INTO ""{Prefix}jobs"" (status, schedule)
                            VALUES ($1, NULL), ($1, $2)", conn))
        {
            insert.Parameters.Add(new NpgsqlParameter { Value = (int)JobStatus.Failed });
            insert.Parameters.Add(new NpgsqlParameter { Value = "*/5 * * * * *" });
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        var storage = DbHelper.CreateDashboardReadStorage(Prefix);
        var stats = await storage.GetStatsAsync(cancellationToken);

        Assert.Equal(1, stats.StatusCounts.Single(c => c.Status == JobStatus.Failed).Count);

        await DropCustomJobsTableAsync(cancellationToken);
    }

    private static async Task RecreateCustomJobsTableAsync(CancellationToken cancellationToken)
    {
        await using var conn = await DbHelper.DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            $@"DROP TABLE IF EXISTS ""{Prefix}jobs"";
               CREATE TABLE ""{Prefix}jobs"" (status int NOT NULL, schedule text NULL);", conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DropCustomJobsTableAsync(CancellationToken cancellationToken)
    {
        await using var conn = await DbHelper.DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand($@"DROP TABLE IF EXISTS ""{Prefix}jobs"";", conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}