using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Queues;
using Jobby.Core.Interfaces.ServerModules.PermanentLockedGroupsCheck;
using Jobby.Core.Models;
using Jobby.Core.Services.ServerModules.PermanentLocksCheck;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jobby.Tests.Core.Services.ServerModules.PermanentLocksCheck;

public class PermanentLockCheckServerModuleTests
{
    private const string ServerId = "ServerId";
    
    private Mock<IPermanentLocksStorage> _storageMock;
    private Mock<IQueueServiceFactory> _queueServiceFactoryMock;
    private Mock<IQueueService<JobWithGroupModel>> _freezingQueueServiceMock;
    private Mock<ITimerService> _timerMock;
    private Mock<ILogger<PermanentLocksCheckServerModule>> _loggerMock;

    public PermanentLockCheckServerModuleTests()
    {
        _storageMock = new Mock<IPermanentLocksStorage>();
        
        _freezingQueueServiceMock = new Mock<IQueueService<JobWithGroupModel>>();
        _queueServiceFactoryMock = new Mock<IQueueServiceFactory>();
        _queueServiceFactoryMock
            .Setup(x => x.Create(It.IsAny<IQueueItemsReader<JobWithGroupModel>>(), It.IsAny<QueueServiceConfig>(), ServerId))
            .Returns(_freezingQueueServiceMock.Object);
        
        _timerMock = new Mock<ITimerService>();
        _loggerMock = new Mock<ILogger<PermanentLocksCheckServerModule>>();
    }

    [Fact]
    public void Constructor_CreatesFreezingQueueServiceWithExpectedConfig()
    {
        var settings = new JobbyServerSettings
        {
            TakeToProcessingBatchSize = 20,
            PermanentLockedFreezingIntervalSeconds = 5,
            DisableSerializableGroups = true,
            Queues =
            [
                new()
                {
                    QueueName = "q1",
                    DisableSerializableGroups = false
                },
                new()
                {
                    QueueName = "q2",
                },
                new()
                {
                    QueueName = "q3",
                    DisableSerializableGroups = true
                }
            ]
        };
        
        QueueServiceConfig? usedQueueServiceConfig = null;
        _queueServiceFactoryMock
            .Setup(x => x.Create(It.IsAny<IQueueItemsReader<JobWithGroupModel>>(), It.IsAny<QueueServiceConfig>(),
                ServerId))
            .Callback<IQueueItemsReader<JobWithGroupModel>, QueueServiceConfig, string>((_, c, _) =>
            {
                usedQueueServiceConfig = c;
            });

        CreatePermanentLocksCheckServerModule(settings);
        
        Assert.NotNull(usedQueueServiceConfig);
        Assert.Equal(settings.PermanentLockedFreezingIntervalSeconds * 1000, usedQueueServiceConfig.WaitingIntervalMaxMs);
        Assert.Equal(settings.PermanentLockedFreezingIntervalSeconds * 1000, usedQueueServiceConfig.WaitingIntervalStartMs);
        Assert.Equal(1, usedQueueServiceConfig.WaitingIntervalFactor);
        Assert.Single(usedQueueServiceConfig.Queues);
        Assert.Equal("q1", usedQueueServiceConfig.Queues[0].QueueName);
        Assert.False(usedQueueServiceConfig.Queues[0].DisableSerializableGroups);
        Assert.Equal(settings.TakeToProcessingBatchSize, usedQueueServiceConfig.Queues[0].MaxBatchSize);
    }

    [Fact]
    public async Task FreezesPermanentLockedAndWaitsBetweenAttempts()
    {
        var settings = new JobbyServerSettings
        {
            TakeToProcessingBatchSize = 10,
            PermanentLockedFreezingIntervalSeconds = 1,
        };
        var serverModule = CreatePermanentLocksCheckServerModule(settings);
        
        var expectedPause = 1000;
        _freezingQueueServiceMock
            .Setup(x => x.GetWaitingIntervalMs())
            .Returns(expectedPause);

        bool freezingCalled = false;
        _freezingQueueServiceMock
            .Setup(x => x.ReadBatch(10, It.IsAny<List<JobWithGroupModel>>()))
            .Callback<int, List<JobWithGroupModel>>((_, _) => freezingCalled = true);
        
        serverModule.Start();
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(100);
            if (freezingCalled)
                break;
        }
        serverModule.SendStopSignal();
        
        _timerMock.Verify(x => x.Delay(expectedPause, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        Assert.True(freezingCalled);
    }

    [Fact]
    public async Task ChecksAndHandlesUnlockingRequests()
    {
        var settings = new JobbyServerSettings
        {
            PermanentLockedHandleUnlockingRequestsIntervalSeconds = 1,
        };
        var serverModule = CreatePermanentLocksCheckServerModule(settings);

        bool handleUnlockingCalled = false;
        _storageMock
            .Setup(x => x.UnfreezeBatchAndUnlockIfAllUnfrozen())
            .Callback(() => handleUnlockingCalled = true);
        
        serverModule.Start();
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(100);
            if (handleUnlockingCalled)
                break;
        }
        serverModule.SendStopSignal();
        
        Assert.True(handleUnlockingCalled);
    }
    
    private PermanentLocksCheckServerModule CreatePermanentLocksCheckServerModule(JobbyServerSettings settings)
    {
        return new PermanentLocksCheckServerModule(_storageMock.Object,
            _queueServiceFactoryMock.Object,
            _timerMock.Object,
            _loggerMock.Object,
            settings,
            ServerId);
    }
}