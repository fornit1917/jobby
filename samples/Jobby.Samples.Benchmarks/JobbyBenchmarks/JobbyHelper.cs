using Jobby.Abstractions.Models;
using Npgsql;

namespace Jobby.Samples.Benchmarks.JobbyBenchmarks;

public static class JobbyHelper
{
    public static void RemoveAllJobs(NpgsqlDataSource dataSource)
    {
        using var conn = dataSource.OpenConnection();
        using var cmd = dataSource.CreateCommand("DELETE FROM jobby_jobs");
        cmd.ExecuteNonQuery();
    }

    public static bool HasNotCompletedJobs(NpgsqlDataSource dataSource)
    {
        using var conn = dataSource.OpenConnection();
        using var cmd = dataSource.CreateCommand($"SELECT id FROM jobby_jobs WHERE status={(int)JobStatus.Scheduled} LIMIT 1");
        var res = cmd.ExecuteReader();
        return res.HasRows;
    }
}