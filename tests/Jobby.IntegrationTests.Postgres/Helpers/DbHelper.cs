using Jobby.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Jobby.IntegrationTests.Postgres.Helpers;

internal static class DbHelper
{
    public static readonly NpgsqlDataSource DataSource = new NpgsqlDataSourceBuilder("Host=localhost;Username=jobby;Password=jobby;Database=jobby_tests_db").Build();
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
        return ctx;
    }

    public static PostgresqlJobbyStorage CreateJobbyStorage()
    {
        return new PostgresqlJobbyStorage(DataSource, new PostgresqlStorageSettings());
    }

    public static async Task DropTablesAsync()
    {
        await using var ctx = CreateContext();
        await ctx.Database.ExecuteSqlRawAsync("DROP TABLE jobby_jobs");
        await ctx.Database.ExecuteSqlRawAsync("DROP TABLE jobby_servers");
    }
}
