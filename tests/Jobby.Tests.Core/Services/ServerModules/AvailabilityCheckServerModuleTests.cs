using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services.ServerModules;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jobby.Tests.Core.Services.ServerModules;

public class AvailabilityCheckServerModuleTests
{
    private readonly Mock<IJobbyStorage> _storageMock = new();
    private readonly Mock<ITimerService>  _timerServiceMock = new();
    private readonly Mock<ILogger<AvailabilityCheckServerModule>> _loggerMock = new();
    
    private const string ServerId = "ServerId";

    [Fact]
    public async Task SendsHeartbeatAndDeletesLostServerAndRestartsStuckJobs()
    {
        var settings = new JobbyServerSettings
        {
            MaxDegreeOfParallelism = 1,
            TakeToProcessingBatchSize = 1,
            DbErrorPauseMs = 1,
            HeartbeatIntervalSeconds = 1,
            MaxNoHeartbeatIntervalSeconds = 10,
        };
        var availabilityCheckServerModule = CreateAvailabilityCheckServerModule(settings);

        var heartbeatSent = false;
        TimeSpan? usedMaxNoHeartbeatInterval = null;
        _storageMock
            .Setup(x => x.SendHeartbeatAsync(ServerId))
            .Callback<string>(_ => heartbeatSent = true);
        _storageMock
            .Setup(x => x.DeleteLostServersAndRestartTheirJobsAsync(It.IsAny<TimeSpan>(), It.IsAny<List<string>>(), It.IsAny<List<StuckJobModel>>()))
            .Callback<TimeSpan, List<string>, List<StuckJobModel>>((interval, _, _) => usedMaxNoHeartbeatInterval = interval);

        availabilityCheckServerModule.Start();

        for (var i = 0; i < 100; i++)
        {
            if (heartbeatSent && usedMaxNoHeartbeatInterval.HasValue)
                break;
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        availabilityCheckServerModule.SendStopSignal();

        Assert.True(heartbeatSent);
        Assert.NotNull(usedMaxNoHeartbeatInterval);
        Assert.Equal(TimeSpan.FromSeconds(settings.MaxNoHeartbeatIntervalSeconds), usedMaxNoHeartbeatInterval.Value);
    }

    private AvailabilityCheckServerModule CreateAvailabilityCheckServerModule(JobbyServerSettings settings)
    {
        return new AvailabilityCheckServerModule(_storageMock.Object,
            _timerServiceMock.Object,
            _loggerMock.Object,
            settings,
            ServerId);
    }
}