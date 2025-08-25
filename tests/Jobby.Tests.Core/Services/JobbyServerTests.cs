using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jobby.Tests.Core.Services;

public class JobbyServerTests
{
    private readonly Mock<IJobbyStorage> _storageMock;
    private readonly Mock<IJobExecutionService> _executionServiceMock;
    private readonly Mock<IJobPostProcessingService> _postProcessingServiceMock;
    private readonly Mock<ILogger<JobbyServer>> _loggerMock;

    private const string ServerId = "serverId";

    public JobbyServerTests()
    {
        _storageMock = new Mock<IJobbyStorage>();
        _executionServiceMock = new Mock<IJobExecutionService>();
        _postProcessingServiceMock = new Mock<IJobPostProcessingService>();
        _loggerMock = new Mock<ILogger<JobbyServer>>();
    }

    [Fact]
    public async Task HappyPath_ExecutesJob()
    {
        var settings = new JobbyServerSettings
        {
            MaxDegreeOfParallelism = 1,
            TakeToProcessingBatchSize = 1,
        };
        using var server = CreateServer(settings);
        var job = new JobExecutionModel();
        var firstCall = true;
        _storageMock
            .Setup(x => x.TakeBatchToProcessingAsync(server.ServerId, settings.TakeToProcessingBatchSize, It.IsAny<List<JobExecutionModel>>()))
            .Callback<string, int, List<JobExecutionModel>>((_, _, jobs) =>
            {
                jobs.Clear();
                if (firstCall)
                {
                    jobs.Add(job);
                    firstCall = false;
                }
            });
        var executed = false;
        _executionServiceMock
            .Setup(x => x.ExecuteJob(job, It.IsAny<CancellationToken>()))
            .Callback<JobExecutionModel, CancellationToken>((_, _) => executed = true);

        _postProcessingServiceMock.SetupGet(x => x.IsRetryQueueEmpty).Returns(true);


        server.StartBackgroundService();

        for (var i = 0; i < 100; i++)
        {
            if (executed)
                break;
            await Task.Delay(10);
        }
        Assert.True(executed);
    }

    [Fact]
    public async Task PostProcessingRetryQueueNotEmpty_RetriesPostProcessing()
    {
        var settings = new JobbyServerSettings
        {
            MaxDegreeOfParallelism = 1,
            TakeToProcessingBatchSize = 1,
            DbErrorPauseMs = 1,
        };
        using var server = CreateServer(settings);
        var postprocessingRetried = false;
        _postProcessingServiceMock.SetupGet(x => x.IsRetryQueueEmpty).Returns(() => false);
        _postProcessingServiceMock
            .Setup(x => x.DoRetriesFromQueue())
            .Callback(() => postprocessingRetried = true);

        server.StartBackgroundService();

        for (var i = 0; i < 100; i++)
        {
            if (postprocessingRetried)
                break;
            await Task.Delay(10);
        }
        Assert.True(postprocessingRetried);
    }

    [Fact]
    public async Task ErrorDuringGetJobs_Retries()
    {
        var settings = new JobbyServerSettings
        {
            MaxDegreeOfParallelism = 1,
            TakeToProcessingBatchSize = 1,
            DbErrorPauseMs = 1,
        };
        using var server = CreateServer(settings);
        var firstCall = true;
        var calledTwoTimes = false;
        _storageMock
            .Setup(x => x.TakeBatchToProcessingAsync(server.ServerId, settings.TakeToProcessingBatchSize, It.IsAny<List<JobExecutionModel>>()))
            .Callback<string, int, List<JobExecutionModel>>((_, _, jobs) =>
            {
                jobs.Clear();
                if (firstCall)
                {
                    firstCall = false;
                    throw new Exception("test error");
                }
                else
                {
                    calledTwoTimes = true;
                }
            });

        _postProcessingServiceMock.SetupGet(x => x.IsRetryQueueEmpty).Returns(true);

        server.StartBackgroundService();

        for (var i = 0; i < 100; i++)
        {
            if (calledTwoTimes)
                break;
            await Task.Delay(10);
        }
        Assert.True(calledTwoTimes);
    }

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
        using var server = CreateServer(settings);

        var heartbeatSent = false;
        DateTime? usedLastHeartbeatForDeleteServers = null;
        _storageMock
            .Setup(x => x.SendHeartbeatAsync(server.ServerId))
            .Callback<string>(_ => heartbeatSent = true);
        _storageMock
            .Setup(x => x.DeleteLostServersAndRestartTheirJobsAsync(It.IsAny<DateTime>(), It.IsAny<List<string>>(), It.IsAny<List<StuckJobModel>>()))
            .Callback<DateTime, List<string>, List<StuckJobModel>>((dt, _, _) => usedLastHeartbeatForDeleteServers = dt);

        server.StartBackgroundService();

        for (var i = 0; i < 100; i++)
        {
            if (heartbeatSent && usedLastHeartbeatForDeleteServers.HasValue)
                break;
            await Task.Delay(10);
        }
        Assert.True(heartbeatSent);
        Assert.NotNull(usedLastHeartbeatForDeleteServers);
        Assert.True(DateTime.UtcNow.Subtract(usedLastHeartbeatForDeleteServers.Value) < TimeSpan.FromSeconds(settings.MaxNoHeartbeatIntervalSeconds + 1));
    }

    private JobbyServer CreateServer(JobbyServerSettings settings)
    {
        return new JobbyServer(_storageMock.Object,
            _executionServiceMock.Object,
            _postProcessingServiceMock.Object,
            _loggerMock.Object,
            settings,
            ServerId);
    }
}
