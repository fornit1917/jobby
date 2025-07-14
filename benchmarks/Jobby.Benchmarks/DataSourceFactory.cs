using Npgsql;

namespace Jobby.Benchmarks;

internal static class DataSourceFactory
{
    public const string ConnectionString = "Host=localhost;Username=test_user;Password=12345;Database=test_db";

    public static NpgsqlDataSource Create(bool enlist=false)
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Enlist = enlist
        };
        var dataSource = NpgsqlDataSource.Create(ConnectionString);
        return dataSource;
    }
}
