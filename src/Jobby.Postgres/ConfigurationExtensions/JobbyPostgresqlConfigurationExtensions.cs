using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Configuration;
using Jobby.Core.Models;
using Npgsql;

namespace Jobby.Postgres.ConfigurationExtensions;

public static class JobbyPostgresqlConfigurationExtensions
{
    public static IJobbyComponentsConfigurable UsePostgresql(this IJobbyComponentsConfigurable opts, Action<IPostgresqlStorageConfigurable> configure)
    {
        var builder = new PostgresqlStorageBuilder();
        configure(builder);
        opts.UseStorage(commonInfra => builder.BuildStorage(commonInfra.ServerSettings));
        opts.UseStorageMigrator(commonInfra => builder.BuildMigrator(commonInfra));
        return opts;
    }

    public static IJobbyComponentsConfigurable UsePostgresql(this IJobbyComponentsConfigurable opts, NpgsqlDataSource dataSource)
    {
        var builder = new PostgresqlStorageBuilder();
        builder.UseDataSource(dataSource);
        opts.UseStorage(commonInfra => builder.BuildStorage(commonInfra.ServerSettings));
        opts.UseStorageMigrator(commonInfra => builder.BuildMigrator(commonInfra));
        return opts;
    }

    [Obsolete("Use IJobbyComponentsConfigurable instead of IJobbyServicesConfigurable")]
    public static IJobbyServicesConfigurable UsePostgresql(this IJobbyServicesConfigurable opts, Action<IPostgresqlStorageConfigurable> configure)
    {
        var builder = new PostgresqlStorageBuilder();
        configure(builder);
        // Note: IJobbyServicesConfigurable doesn't support factory pattern, using default settings
        opts.UseStorage(builder.BuildStorage(new JobbyServerSettings()));
        return opts;
    }

    [Obsolete("Use IJobbyComponentsConfigurable instead of IJobbyServicesConfigurable")]
    public static IJobbyServicesConfigurable UsePostgresql(this IJobbyServicesConfigurable opts, NpgsqlDataSource dataSource)
    {
        var builder = new PostgresqlStorageBuilder();
        builder.UseDataSource(dataSource);
        // Note: IJobbyServicesConfigurable doesn't support factory pattern, using default settings
        opts.UseStorage(builder.BuildStorage(new JobbyServerSettings()));
        return opts;
    }
}
