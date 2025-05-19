using Jobby.Core.Interfaces.Builders;
using Npgsql;

namespace Jobby.Postgres.ConfigurationExtensions;

public static class JobbyPostgresqlConfigurationExtensions
{
    public static IJobbyServicesConfigurable UsePostgresql(this IJobbyServicesConfigurable opts, Action<IPostgresStorageConfigurable> configure)
    {
        var builder = new PgStorageBuilder();
        configure(builder);
        var storage = builder.Build();
        opts.UseStorage(storage);
        return opts;
    }

    public static IJobbyServicesConfigurable UsePostgresql(this IJobbyServicesConfigurable opts, NpgsqlDataSource dataSource)
    {
        var builder = new PgStorageBuilder();
        builder.UseDataSource(dataSource);
        var storage = builder.Build();
        opts.UseStorage(storage);
        return opts;
    }
}
