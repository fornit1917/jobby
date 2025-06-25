using Jobby.Postgres;
using Jobby.Postgres.Helpers;

namespace Jobby.Tests.Postgres.Helpers;

public class TableNameTests
{
    [Fact]
    public void SchemaEmpty_ReturnsOnlyTableName()
    {
        var settings = new PostgresqlStorageSettings { TablesPrefix = "prefix_" };
        Assert.Equal("\"prefix_jobs\"", TableName.For("jobs", settings));
    }

    [Fact]
    public void SchemaNotEmpty_ReturnTableNameWithSchema()
    {
        var settings = new PostgresqlStorageSettings
        {
            TablesPrefix = "prefix_",
            SchemaName = "jobby"
        };
        Assert.Equal("\"jobby\".\"prefix_jobs\"", TableName.For("jobs", settings));
    }
}
