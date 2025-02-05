using Hangfire.PostgreSql;
using Npgsql;

namespace Jobby.Samples.Benchmarks.HangfireBenchmarks;

internal class HangfireConnectionFactory : IConnectionFactory
{
    private readonly NpgsqlDataSource _dataSource;

    public HangfireConnectionFactory(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public NpgsqlConnection GetOrCreateConnection()
    {
        return _dataSource.OpenConnection();
    }
}
