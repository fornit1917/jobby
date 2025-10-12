using Jobby.Core.Models;

namespace Jobby.Tests.Core.Models;

public class JobbyServerSettingsTests
{
    [Fact]
    public void PollingInterval_IncreasingSettingsNotSpecified_ReturnsStartEqualsToMax()
    {
        var settings = new JobbyServerSettings
        {
            PollingIntervalMs = 1000
        };

        Assert.Equal(1000, settings.PollingIntervalMs);
        Assert.Equal(1000, settings.PollingIntervalStartMs);
        Assert.Equal(2, settings.PollingIntervalFactor);
    }

    [Fact]
    public void PollingInterval_StartSpecifiedFactorNotSpecified_ReturstFactorAsTwo()
    {
        var settings = new JobbyServerSettings
        {
            PollingIntervalMs = 1000,
            PollingIntervalStartMs = 100,
        };

        Assert.Equal(1000, settings.PollingIntervalMs);
        Assert.Equal(100, settings.PollingIntervalStartMs);
        Assert.Equal(2, settings.PollingIntervalFactor);
    }

    [Fact]
    public void PollingInterval_IncreasingSettingsSpecified_ReturnsSpecifiedSettings()
    {
        var settings = new JobbyServerSettings
        {
            PollingIntervalMs = 1000,
            PollingIntervalStartMs = 100,
            PollingIntervalFactor = 5,
        };

        Assert.Equal(1000, settings.PollingIntervalMs);
        Assert.Equal(100, settings.PollingIntervalStartMs);
        Assert.Equal(5, settings.PollingIntervalFactor);
    }
}
