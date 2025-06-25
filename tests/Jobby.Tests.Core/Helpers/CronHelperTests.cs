using Jobby.Core.Exceptions;
using Cronos;
using Jobby.Core.Helpers;

namespace Jobby.Tests.Core.Helpers;

public class CronHelperTests
{
    [Fact]
    public void ParsesStandardFormat()
    {
        var cron = "*/10 * * * *";
        var from = DateTime.UtcNow;
        var expectedResult = CronExpression.Parse(cron, CronFormat.Standard).GetNextOccurrence(from);

        var actualResult = CronHelper.GetNext(cron, from);

        Assert.Equal(expectedResult, actualResult);
    }

    [Fact]
    public void ParsesIncludeSecondsFormat()
    {
        var cron = "*/10 * * * * *";
        var from = DateTime.UtcNow;
        var expectedResult = CronExpression.Parse(cron, CronFormat.IncludeSeconds).GetNextOccurrence(from);

        var actualResult = CronHelper.GetNext(cron, from);

        Assert.Equal(expectedResult, actualResult);
    }
}
