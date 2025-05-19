using Jobby.Postgres.Helpers;

namespace Jobby.Postgres.Tests.Helpers;

public class TableNameTests
{
    [Fact]
    public void SchemaEmpty_ReturnsOnlyTableName()
    {
        var settings = new PgStorageSettings { TablesPrefix = "prefix_" };
        Assert.Equal("\"prefix_jobs\"", TableName.For("jobs", settings));
    }

    [Fact]
    public void SchemaNotEmpty_ReturnTableNameWithSchema()
    {
        var settings = new PgStorageSettings 
        { 
            TablesPrefix = "prefix_",
            SchemaName = "jobby"
        };
        Assert.Equal("\"jobby\".\"prefix_jobs\"", TableName.For("jobs", settings));
    }
}
