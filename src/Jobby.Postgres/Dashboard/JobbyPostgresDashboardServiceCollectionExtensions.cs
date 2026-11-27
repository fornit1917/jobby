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
        ArgumentNullException.ThrowIfNull(dataSource);
        return services.AddJobbyPostgresDashboardStorage(_ => dataSource, configure);
    }

    /// <summary>
    /// Takes the data source from the application's services, for example
    /// <c>sp => sp.GetRequiredService&lt;NpgsqlDataSource&gt;()</c> for a data source registered in the container.
    /// </summary>
    public static IServiceCollection AddJobbyPostgresDashboardStorage(
        this IServiceCollection services,
        Func<IServiceProvider, NpgsqlDataSource> dataSourceFactory,
        Action<PostgresqlDashboardStorageOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(dataSourceFactory);

        var options = new PostgresqlDashboardStorageOptions();
        configure?.Invoke(options);

        var settings = new PostgresqlStorageSettings
        {
            SchemaName = options.SchemaName,
            TablesPrefix = options.TablesPrefix,
        };

        // The factory runs once: a factory that creates the data source must not open a connection pool per storage.
        services.AddSingleton(sp =>
        {
            var dataSource = dataSourceFactory(sp);

            // One instance for both storages: management actions drop the counts they have just changed.
            var countsCache = new QueueStatusCountsCache(dataSource, settings, options.CountsCacheDuration);

            return new Storages(
                new PostgresqlJobbyDashboardReadStorage(dataSource, settings, countsCache),
                new PostgresqlJobbyDashboardCommandStorage(dataSource, settings, countsCache));
        });

        services.AddSingleton<IJobbyDashboardReadStorage>(sp => sp.GetRequiredService<Storages>().Read);
        services.AddSingleton<IJobbyDashboardCommandStorage>(sp => sp.GetRequiredService<Storages>().Command);
        return services;
    }

    private sealed record Storages(
        PostgresqlJobbyDashboardReadStorage Read,
        PostgresqlJobbyDashboardCommandStorage Command);
}
