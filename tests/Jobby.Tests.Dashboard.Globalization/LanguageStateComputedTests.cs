using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client;
using Jobby.Dashboard.Client.Localization;

namespace Jobby.Tests.Dashboard.Globalization;

public class LanguageStateComputedTests
{
    private static LanguageState New() => new(new DashboardClientConfig());

    [Fact]
    public void Relative_PastEnglishIsAbbreviated()
    {
        var l = New();
        var past = DateTime.UtcNow.AddMinutes(-5);
        Assert.Equal("5m ago", l.Relative(past));
    }

    [Fact]
    public void Relative_PastRussianUsesSpacedSuffix()
    {
        var l = New();
        l.ApplyForTests(DashboardLanguage.Russian);
        Assert.Equal("5 мин назад", l.Relative(DateTime.UtcNow.AddMinutes(-5)));
    }

    [Fact]
    public void Relative_FuturePortuguese()
    {
        var l = New();
        l.ApplyForTests(DashboardLanguage.PortugueseBrazil);
        Assert.Equal("em 2 h", l.Relative(DateTime.UtcNow.AddHours(2).AddMinutes(1)));
    }

    [Fact]
    public void Relative_SubSecondIsJustNow()
    {
        var l = New();
        Assert.Equal("just now", l.Relative(DateTime.UtcNow));
    }

    [Fact]
    public void Relative_DisallowFutureClampsToJustNow()
    {
        var l = New();
        Assert.Equal("just now", l.Relative(DateTime.UtcNow.AddHours(1), allowFuture: false));
    }

    [Theory]
    [InlineData(JobStatus.Scheduled, "Scheduled")]
    [InlineData(JobStatus.Failed, "Failed")]
    public void Status_English(JobStatus s, string expected) => Assert.Equal(expected, New().Status(s));

    [Fact]
    public void Status_Russian()
    {
        var l = New();
        l.ApplyForTests(DashboardLanguage.Russian);
        Assert.Equal("Ошибка", l.Status(JobStatus.Failed));
    }

    [Fact]
    public void Status_EveryValueResolvesInEveryLanguage()
    {
        var l = New();
        foreach (var lang in DashboardLanguages.All)
        {
            l.ApplyForTests(lang);
            foreach (var s in Enum.GetValues<JobStatus>())
            {
                var text = l.Status(s);
                Assert.False(string.IsNullOrWhiteSpace(text));
                Assert.NotEqual($"status.{s}", text);
            }
        }
    }

    [Fact]
    public void Role_RussianAndEveryValueResolves()
    {
        var l = New();
        l.ApplyForTests(DashboardLanguage.Russian);
        Assert.Equal("Локер", l.Role(LockedGroupJobRole.Locker));

        foreach (var lang in DashboardLanguages.All)
        {
            l.ApplyForTests(lang);
            foreach (var r in Enum.GetValues<LockedGroupJobRole>())
            {
                var text = l.Role(r);
                Assert.False(string.IsNullOrWhiteSpace(text));
                Assert.NotEqual($"role.{r}", text);
            }
        }
    }
}
