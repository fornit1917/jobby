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
        opts.UseStorage(builder.BuildStorage());
        opts.UsePermanentLocksStorage(_ => builder.BuildPermanentLocksStorage());
        opts.UseStorageMigrator(commonInfra => builder.BuildMigrator(commonInfra));
        return opts;
    }

    public static IJobbyComponentsConfigurable UsePostgresql(this IJobbyComponentsConfigurable opts, NpgsqlDataSource dataSource)
    {
        var builder = new PostgresqlStorageBuilder();
        builder.UseDataSource(dataSource);
        opts.UseStorage(builder.BuildStorage());
        opts.UsePermanentLocksStorage(_ => builder.BuildPermanentLocksStorage());
        opts.UseStorageMigrator(commonInfra => builder.BuildMigrator(commonInfra));
        return opts;
    }
}
