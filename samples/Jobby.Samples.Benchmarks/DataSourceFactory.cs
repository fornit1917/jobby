using Npgsql;

namespace Jobby.Samples.Benchmarks;

internal static class DataSourceFactory
{
    public static NpgsqlDataSource Create()
    {
        var connectionString = "Host=localhost;Username=test_user;Password=12345;Database=test_db";
        var dataSource = NpgsqlDataSource.Create(connectionString);
        return dataSource;
    }
}
