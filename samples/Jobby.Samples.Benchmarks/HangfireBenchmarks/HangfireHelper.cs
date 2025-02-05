using Hangfire;
using Hangfire.PostgreSql;
using Jobby.Abstractions.Models;
using Npgsql;

namespace Jobby.Samples.Benchmarks.HangfireBenchmarks;

internal static class HangfireHelper
{
    public static void ConfigureGlobal(NpgsqlDataSource dataSource)
    {
        GlobalConfiguration.Configuration.UsePostgreSqlStorage(x =>
        {
            x.UseConnectionFactory(new HangfireConnectionFactory(dataSource));  
        }, new PostgreSqlStorageOptions
        {
            QueuePollInterval = TimeSpan.FromSeconds(1),
        });
    }

    public static void DropHangfireTables(NpgsqlDataSource dataSource)
    {
        using var conn = dataSource.CreateConnection();
        using var cmd = dataSource.CreateCommand($"DROP SCHEMA hangfire CASCADE;");
        cmd.ExecuteNonQuery();
    }

    public static bool HasNotCompletedJobs(NpgsqlDataSource dataSource)
    {
        using var conn = dataSource.CreateConnection();
        using var cmd = dataSource.CreateCommand($"SELECT id FROM hangfire.job WHERE statename='Enqueued' LIMIT 1");
        var res = cmd.ExecuteReader();
        return res.HasRows;
    }
}
