using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services.ServerModules;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jobby.Tests.Core.Services.AvailabilityCheck;

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
        DateTime? usedLastHeartbeatForDeleteServers = null;
        _storageMock
            .Setup(x => x.SendHeartbeatAsync(ServerId))
            .Callback<string>(_ => heartbeatSent = true);
        _storageMock
            .Setup(x => x.DeleteLostServersAndRestartTheirJobsAsync(It.IsAny<DateTime>(), It.IsAny<List<string>>(), It.IsAny<List<StuckJobModel>>()))
            .Callback<DateTime, List<string>, List<StuckJobModel>>((dt, _, _) => usedLastHeartbeatForDeleteServers = dt);

        availabilityCheckServerModule.Start();

        for (var i = 0; i < 100; i++)
        {
            if (heartbeatSent && usedLastHeartbeatForDeleteServers.HasValue)
                break;
            await Task.Delay(10);
        }
        availabilityCheckServerModule.SendStopSignal();
        
        Assert.True(heartbeatSent);
        Assert.NotNull(usedLastHeartbeatForDeleteServers);
        Assert.True(DateTime.UtcNow.Subtract(usedLastHeartbeatForDeleteServers.Value) < TimeSpan.FromSeconds(settings.MaxNoHeartbeatIntervalSeconds + 1));
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