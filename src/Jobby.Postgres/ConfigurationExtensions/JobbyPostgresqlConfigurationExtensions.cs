using Jobby.Core.Interfaces.Builders;
using Npgsql;

namespace Jobby.Postgres.ConfigurationExtensions;

public static class JobbyPostgresqlConfigurationExtensions
{
    public static IJobbyServicesConfigurable UsePostgresql(this IJobbyServicesConfigurable opts, NpgsqlDataSource dataSource)
    {
        var storage = new PgJobsStorage(dataSource);
        opts.UseStorage(storage);
        return opts;
    }
}
