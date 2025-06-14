using Jobby.Core.Interfaces;
using Npgsql;

namespace Jobby.Postgres.ConfigurationExtensions;

public static class JobbyPostgresqlConfigurationExtensions
{
    public static IJobbyServicesConfigurable UsePostgresql(this IJobbyServicesConfigurable opts, Action<IPostgresqlStorageConfigurable> configure)
    {
        var builder = new PostgresqlStorageBuilder();
        configure(builder);
        var storage = builder.Build();
        opts.UseStorage(storage);
        return opts;
    }

    public static IJobbyServicesConfigurable UsePostgresql(this IJobbyServicesConfigurable opts, NpgsqlDataSource dataSource)
    {
        var builder = new PostgresqlStorageBuilder();
        builder.UseDataSource(dataSource);
        var storage = builder.Build();
        opts.UseStorage(storage);
        return opts;
    }
}
