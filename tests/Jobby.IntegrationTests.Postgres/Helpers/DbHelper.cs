using Jobby.Postgres;
using Jobby.Postgres.Dashboard;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Jobby.IntegrationTests.Postgres.Helpers;

internal static class DbHelper
{
    public static readonly NpgsqlDataSource DataSource = new NpgsqlDataSourceBuilder("Host=localhost;Username=jobby;Password=jobby;Database=jobby_tests_db;GSS Encryption Mode=Disable").Build();
    private static readonly DbContextOptions Options = new DbContextOptionsBuilder().UseNpgsql(DataSource).Options;

    public static JobbyTestingDbContext CreateContext()
    {
        return new JobbyTestingDbContext(Options);
    }

    public static JobbyTestingDbContext CreateContextAndClearDb()
    {
        var ctx = CreateContext();
        ctx.Jobs.Where(x => true).ExecuteDelete();
        ctx.Servers.Where(x => true).ExecuteDelete();
        return ctx;
    }

    public static async Task<JobbyTestingDbContext> CreateContextAndClearDbAsync()
    {
        var ctx = CreateContext();
        await ctx.Jobs.Where(x => true).ExecuteDeleteAsync();
        await ctx.Servers.Where(x => true).ExecuteDeleteAsync();
        await ctx.UnlockingGroups.Where(x => true).ExecuteDeleteAsync();
        return ctx;
    }

    public static PostgresqlJobbyStorage CreateJobbyStorage()
    {
        return new PostgresqlJobbyStorage(DataSource, new PostgresqlStorageSettings());
    }

    public static PostgresqlPermanentLocksStorage CreatePermanentLockedGroupsStorage()
    {
        return new PostgresqlPermanentLocksStorage(DataSource, new PostgresqlStorageSettings());
    }

    public static PostgresqlJobbyDashboardReadStorage CreateDashboardReadStorage()
    {
        return CreateDashboardStorages(TimeSpan.Zero).Read;
    }

    public static PostgresqlJobbyDashboardReadStorage CreateDashboardReadStorage(string tablesPrefix)
    {
        var settings = new PostgresqlStorageSettings { TablesPrefix = tablesPrefix };
        return new PostgresqlJobbyDashboardReadStorage(DataSource, settings,
            new QueueStatusCountsCache(DataSource, settings, TimeSpan.Zero));
    }

    public static PostgresqlJobbyDashboardCommandStorage CreateDashboardCommandStorage()
    {
        return CreateDashboardStorages(TimeSpan.Zero).Command;
    }

    // Read and command storages sharing one counts cache, as AddJobbyPostgresDashboardStorage registers them.
    public static (PostgresqlJobbyDashboardReadStorage Read, PostgresqlJobbyDashboardCommandStorage Command)
        CreateDashboardStorages(TimeSpan countsCacheDuration, TimeProvider? timeProvider = null)
    {
        var settings = new PostgresqlStorageSettings();
        var countsCache = new QueueStatusCountsCache(DataSource, settings, countsCacheDuration, timeProvider);
        return (new PostgresqlJobbyDashboardReadStorage(DataSource, settings, countsCache),
            new PostgresqlJobbyDashboardCommandStorage(DataSource, settings, countsCache));
    }
}
