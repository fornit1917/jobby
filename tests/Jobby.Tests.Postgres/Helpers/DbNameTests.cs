using Jobby.Postgres;
using Jobby.Postgres.Helpers;

namespace Jobby.Tests.Postgres.Helpers;

public class DbNameTests
{
    [Fact]
    public void SchemaEmpty_ReturnsOnlyTableName()
    {
        var settings = new PostgresqlStorageSettings { TablesPrefix = "prefix_" };
        Assert.Equal("\"prefix_jobs\"", DbName.For("jobs", settings));
    }

    [Fact]
    public void SchemaNotEmpty_ReturnTableNameWithSchema()
    {
        var settings = new PostgresqlStorageSettings
        {
            TablesPrefix = "prefix_",
            SchemaName = "jobby"
        };
        Assert.Equal("\"jobby\".\"prefix_jobs\"", DbName.For("jobs", settings));
    }

    [Fact]
    public void For_QuoteInSchemaName_PinsCurrentBehaviour()
    {
        // DbName does not escape embedded double quotes, it only wraps parts in quotes.
        // Such a schema name would produce invalid SQL — pinned so a future fix is deliberate.
        var settings = new PostgresqlStorageSettings { SchemaName = "sc\"hema", TablesPrefix = "jobby_" };

        Assert.Equal("\"sc\"hema\".\"jobby_jobs\"", DbName.For("jobs", settings));
    }

    [Fact]
    public void HelperMethods_UseGivenSettings()
    {
        var settings = new PostgresqlStorageSettings { SchemaName = "s", TablesPrefix = "p_" };

        Assert.Equal("\"s\".\"p_jobs\"", DbName.Jobs(settings));
        Assert.Equal("\"s\".\"p_servers\"", DbName.Servers(settings));
        Assert.Equal("\"s\".\"p_unlocking_groups\"", DbName.UnlockingGroups(settings));
        Assert.Equal("\"s\".\"p_take_to_processing\"", DbName.TakeToProcessing(settings));
    }
}
