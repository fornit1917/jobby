using Jobby.Postgres;

namespace Jobby.Tests.Postgres;

public class PostgresqlStorageSettingsTests
{
    [Fact]
    public void HaveSensibleDefaults()
    {
        var settings = new PostgresqlStorageSettings();

        Assert.Equal(string.Empty, settings.SchemaName);
        Assert.Equal("jobby_", settings.TablesPrefix);
    }
}
