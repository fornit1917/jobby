using Jobby.Postgres.Dashboard;

namespace Jobby.Tests.Postgres.Dashboard;

public class PostgresqlDashboardStorageOptionsTests
{
    [Fact]
    public void CountsCacheDuration_Negative_IsRejected()
    {
        var options = new PostgresqlDashboardStorageOptions();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => options.CountsCacheDuration = TimeSpan.FromSeconds(-1));

        Assert.StartsWith("The duration cannot be negative.", exception.Message);
    }

    [Fact]
    public void CountsCacheDuration_Zero_IsAcceptedToDisableReuse()
    {
        var options = new PostgresqlDashboardStorageOptions { CountsCacheDuration = TimeSpan.Zero };

        Assert.Equal(TimeSpan.Zero, options.CountsCacheDuration);
    }
}
