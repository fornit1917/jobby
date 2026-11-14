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

    public static TheoryData<string, Func<int, JobbyServerSettings>, Func<JobbyServerSettings, int>> NumericProperties => new()
    {
        { "PollingIntervalMs", value => new JobbyServerSettings { PollingIntervalMs = value }, x => x.PollingIntervalMs },
        { "PollingIntervalStartMs", value => new JobbyServerSettings { PollingIntervalStartMs = value }, x => x.PollingIntervalStartMs },
        { "PollingIntervalFactor", value => new JobbyServerSettings { PollingIntervalFactor = value }, x => x.PollingIntervalFactor },
        { "DbErrorPauseMs", value => new JobbyServerSettings { DbErrorPauseMs = value }, x => x.DbErrorPauseMs },
        { "MaxDegreeOfParallelism", value => new JobbyServerSettings { MaxDegreeOfParallelism = value }, x => x.MaxDegreeOfParallelism },
        { "TakeToProcessingBatchSize", value => new JobbyServerSettings { TakeToProcessingBatchSize = value }, x => x.TakeToProcessingBatchSize },
        { "HeartbeatIntervalSeconds", value => new JobbyServerSettings { HeartbeatIntervalSeconds = value }, x => x.HeartbeatIntervalSeconds },
        { "MaxNoHeartbeatIntervalSeconds", value => new JobbyServerSettings { MaxNoHeartbeatIntervalSeconds = value }, x => x.MaxNoHeartbeatIntervalSeconds },
        { "PermanentLockedFreezingIntervalSeconds", value => new JobbyServerSettings { PermanentLockedFreezingIntervalSeconds = value }, x => x.PermanentLockedFreezingIntervalSeconds },
        { "PermanentLockedHandleUnlockingRequestsIntervalSeconds", value => new JobbyServerSettings { PermanentLockedHandleUnlockingRequestsIntervalSeconds = value }, x => x.PermanentLockedHandleUnlockingRequestsIntervalSeconds },
    };

    [Theory]
    [MemberData(nameof(NumericProperties))]
    public void NumericProperty_RejectsNonPositiveAndStoresPositiveValues(string propertyName, Func<int, JobbyServerSettings> create, Func<JobbyServerSettings, int> get)
    {
        var zeroException = Assert.Throws<ArgumentException>(() => create(0));
        Assert.Contains(propertyName, zeroException.Message);

        var negativeException = Assert.Throws<ArgumentException>(() => create(-5));
        Assert.Contains(propertyName, negativeException.Message);

        var settings = create(7);
        Assert.Equal(7, get(settings));
    }
}
