using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Jobby.Postgres.IntegrationTests.Helpers;

internal static class DbHelper
{
    public static readonly NpgsqlDataSource DataSource = new NpgsqlDataSourceBuilder("Host=localhost;Username=jobby;Password=jobby;Database=jobby_tests_db").Build();
    public static DbContextOptions Options = new DbContextOptionsBuilder().UseNpgsql(DataSource).Options;

    public static JobbyDbContext CreateContext()
    {
        return new JobbyDbContext(Options);
    }

    public static JobbyDbContext CreateContextAndClearJobsTable()
    {
        var ctx = CreateContext();
        ctx.Jobs.Where(x => true).ExecuteDelete();
        return ctx;
    }

    public static async Task<JobbyDbContext> CreateContextAndClearJobsTableAsync()
    {
        var ctx = CreateContext();
        await ctx.Jobs.Where(x => true).ExecuteDeleteAsync();
        return ctx;
    }

    public static PostgresqlJobbyStorage CreateJobbyStorage()
    {
        return new PostgresqlJobbyStorage(DataSource, new PostgresqlStorageSettings());
    }
}
