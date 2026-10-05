using System.Diagnostics.CodeAnalysis;
using Jobby.Core.Interfaces.Dashboard;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Jobby.Postgres.Dashboard;

public static class JobbyPostgresDashboardServiceCollectionExtensions
{
    [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Global")]
    public static IServiceCollection AddJobbyPostgresDashboardStorage(
        this IServiceCollection services,
        NpgsqlDataSource dataSource,
        Action<PostgresqlDashboardStorageOptions>? configure = null)
    {
        var options = new PostgresqlDashboardStorageOptions();
        configure?.Invoke(options);

        var settings = new PostgresqlStorageSettings
        {
            SchemaName = options.SchemaName,
            TablesPrefix = options.TablesPrefix,
        };

        services.AddSingleton<IJobbyDashboardReadStorage>(_ =>
            new PostgresqlJobbyDashboardReadStorage(dataSource, settings));

        services.AddSingleton<IJobbyDashboardCommandStorage>(_ =>
            new PostgresqlJobbyDashboardCommandStorage(dataSource, settings));
        return services;
    }
}