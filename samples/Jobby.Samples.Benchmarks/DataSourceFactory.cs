using Npgsql;

namespace Jobby.Samples.Benchmarks;

internal static class DataSourceFactory
{
    public static NpgsqlDataSource Create()
    {
        var connectionString = "Host=localhost;Username=test_user;Password=12345;Database=test_db";
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = true,
            MaxPoolSize = 20,
            MinPoolSize = 5,
            ConnectionIdleLifetime = 30
        };
        var dataSource = NpgsqlDataSource.Create(connectionString);
        return dataSource;
    }
}
