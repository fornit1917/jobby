using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Configuration;
using Npgsql;

namespace Jobby.Postgres.ConfigurationExtensions;

public static class JobbyPostgresqlConfigurationExtensions
{
    public static IJobbyComponentsConfigurable UsePostgresql(this IJobbyComponentsConfigurable opts, Action<IPostgresqlStorageConfigurable> configure)
    {
        var builder = new PostgresqlStorageBuilder();
        configure(builder);
        var storage = builder.Build();
        opts.UseStorage(storage);
        return opts;
    }

    public static IJobbyComponentsConfigurable UsePostgresql(this IJobbyComponentsConfigurable opts, NpgsqlDataSource dataSource)
    {
        var builder = new PostgresqlStorageBuilder();
        builder.UseDataSource(dataSource);
        var storage = builder.Build();
        opts.UseStorage(storage);
        return opts;
    }

    [Obsolete("Use IJobbyComponentsConfigurable instead of IJobbyServicesConfigurable")]
    public static IJobbyServicesConfigurable UsePostgresql(this IJobbyServicesConfigurable opts, Action<IPostgresqlStorageConfigurable> configure)
    {
        var builder = new PostgresqlStorageBuilder();
        configure(builder);
        var storage = builder.Build();
        opts.UseStorage(storage);
        return opts;
    }

    [Obsolete("Use IJobbyComponentsConfigurable instead of IJobbyServicesConfigurable")]
    public static IJobbyServicesConfigurable UsePostgresql(this IJobbyServicesConfigurable opts, NpgsqlDataSource dataSource)
    {
        var builder = new PostgresqlStorageBuilder();
        builder.UseDataSource(dataSource);
        var storage = builder.Build();
        opts.UseStorage(storage);
        return opts;
    }
}
