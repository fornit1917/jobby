using Jobby.Core.Interfaces.Dashboard;
using Jobby.Postgres.Dashboard;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Jobby.Tests.Postgres.Dashboard;

public class JobbyPostgresDashboardServiceCollectionExtensionsTests
{
    [Fact]
    public async Task DataSourceFactory_RunsOnceWithTheApplicationServices_WhenAStorageIsFirstResolved()
    {
        // No connection is opened: the data source is only handed over to the storages.
        await using var dataSource = NpgsqlDataSource.Create("Host=localhost");
        var services = new ServiceCollection();
        services.AddSingleton(dataSource);
        var resolved = new List<NpgsqlDataSource>();

        services.AddJobbyPostgresDashboardStorage(sp =>
        {
            resolved.Add(sp.GetRequiredService<NpgsqlDataSource>());
            return resolved[^1];
        });

        Assert.Empty(resolved);
        await using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IJobbyDashboardReadStorage>();
        provider.GetRequiredService<IJobbyDashboardCommandStorage>();
        Assert.Same(dataSource, Assert.Single(resolved));
    }

    [Fact]
    public void DataSource_Null_IsRejectedAtRegistration()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentNullException>(
            () => services.AddJobbyPostgresDashboardStorage((NpgsqlDataSource)null!));

        Assert.Equal("dataSource", exception.ParamName);
    }
}
