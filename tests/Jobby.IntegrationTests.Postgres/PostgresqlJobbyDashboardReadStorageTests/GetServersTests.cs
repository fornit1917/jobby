using Jobby.IntegrationTests.Postgres.Helpers;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyDashboardReadStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class GetServersTests
{
    [Fact]
    public async Task GetServersAsync_ReturnsServersOrderedByIdWithHeartbeats()
    {
        await using var db = await DbHelper.CreateContextAndClearDbAsync();
        var hbA = DateTime.UtcNow.AddSeconds(-1);
        var hbB = DateTime.UtcNow.AddMinutes(-30);
        db.Servers.AddRange(
            new ServerDbModel { Id = "server-b", HeartbeatTs = hbB },
            new ServerDbModel { Id = "server-a", HeartbeatTs = hbA });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateDashboardReadStorage();
        var servers = await storage.GetServersAsync(TestContext.Current.CancellationToken);

        Assert.Collection(servers,
            s =>
            {
                Assert.Equal("server-a", s.Id);
                Assert.Equal(hbA, s.HeartbeatTs, TimeSpan.FromSeconds(1));
            },
            s =>
            {
                Assert.Equal("server-b", s.Id);
                Assert.Equal(hbB, s.HeartbeatTs, TimeSpan.FromSeconds(1));
            });
    }
}