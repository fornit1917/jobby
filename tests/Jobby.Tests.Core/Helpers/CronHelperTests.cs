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

    [Fact]
    public void GetNext_SixFieldsWithTrailingSpace_ParsesIncludeSecondsFormat()
    {
        var cron = "*/10 * * * * * ";
        var from = DateTime.UtcNow;
        var expectedResult = CronExpression.Parse("*/10 * * * * *", CronFormat.IncludeSeconds).GetNextOccurrence(from);

        var actualResult = CronHelper.GetNext(cron, from);

        Assert.Equal(expectedResult, actualResult);
    }

    [Fact]
    public void GetNext_FiveFieldsWithTrailingSpace_ParsesStandardFormat()
    {
        var cron = "*/10 * * * * ";
        var from = DateTime.UtcNow;
        var expectedResult = CronExpression.Parse("*/10 * * * *", CronFormat.Standard).GetNextOccurrence(from);

        var actualResult = CronHelper.GetNext(cron, from);

        Assert.Equal(expectedResult, actualResult);
    }

    [Theory]
    [InlineData("   ")]           // spaces only
    [InlineData("5")]             // single component
    [InlineData("* * * *")]       // only 4 components
    [InlineData("* * * * * * *")] // 7 components
    public void EnsureCronExpressionIsValid_InvalidCronExpression_ThrowsInvalidScheduleException(string cron)
    {
        Assert.Throws<InvalidScheduleException>(() => CronHelper.EnsureCronExpressionIsValid(cron));
    }

    [Theory]
    [InlineData("*/10 * * * *")]
    [InlineData("*/10 * * * * *")]
    [InlineData("*/10 * * * * ")]
    [InlineData("*/10 * * * * * ")]
    [InlineData(" 0 */10 * * *")]
    [InlineData("*/10  * * * *")] // double space between fields
    public void EnsureCronExpressionIsValid_ValidCronExpression_DoesNotThrow(string cron)
    {
        CronHelper.EnsureCronExpressionIsValid(cron);
    }
}
