using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services.Queues;
using Jobby.TestsUtils.Mocks;
using Moq;

namespace Jobby.Tests.Core.Services.Queues;

public class SingleQueueServiceTests
{
    private const string ServerId = "ServerId";
    private readonly Mock<IJobbyStorage> _storageMock = new();
    private readonly Mock<ITimerService> _timerMock = new();

    [Fact]
    public void WaitIfEmpty_Unknown_DoesNotDelay()
    {
        var settings = new JobbyServerSettings();
        var queueService = CreateQueueService(settings);
        
        var waitingTask = queueService.WaitIfEmpty();
        
        Assert.Equal(Task.CompletedTask, waitingTask);
        _timerMock.Verify(x => x.Delay(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task WaitIfEmpty_NotEmpty_DoesNotDelay()
    {
        var batchSize = 5;
        var jobs = new List<JobExecutionModel>()
        {
            new()
        };
        _storageMock.SetupTakeToProcessing(ServerId, batchSize, QueueSettings.DefaultQueueName, jobs);
        
        var settings = new JobbyServerSettings();
        var queueService = CreateQueueService(settings);

        await queueService.TakeBatchToProcessing(batchSize, new List<JobExecutionModel>());
        var waitingTask = queueService.WaitIfEmpty();
        
        Assert.Equal(Task.CompletedTask, waitingTask);
        _timerMock.Verify(x => x.Delay(It.IsAny<int>()), Times.Never);
    }
    
    [Fact]
    public async Task WaitIfEmpty_Empty_DoesDelay()
    {
        var batchSize = 5;
        var jobs = new List<JobExecutionModel>();
        _storageMock.SetupTakeToProcessing(ServerId, batchSize, QueueSettings.DefaultQueueName, jobs);
        
        var settings = new JobbyServerSettings();
        var queueService = CreateQueueService(settings);

        await queueService.TakeBatchToProcessing(batchSize, new List<JobExecutionModel>());
        var waitingTask = queueService.WaitIfEmpty();
        
        Assert.NotEqual(Task.CompletedTask, waitingTask);
        _timerMock.Verify(x => x.Delay(settings.PollingIntervalMs), Times.Once);
    }

    [Fact]
    public async Task TakeBatchToProcessing_DefaultQueueSettings_TakesJobsDefaultQueueSettings()
    {
        var batchSize = 5;
        var settings = new JobbyServerSettings();
        var queueService = CreateQueueService(settings);
        
        await queueService.TakeBatchToProcessing(batchSize, new List<JobExecutionModel>());

        _storageMock.VerifyTakeToProcessing(ServerId, batchSize, QueueSettings.DefaultQueueName);
    }

    [Fact]
    public async Task TakeBatchToProcessing_SpecialQueueSettings_TakesJobsWithSpecialQueueSettings()
    {
        var batchSize = 5;
        var queueSettings = new QueueSettings
        {
            QueueName = "qname",
            MaxDegreeOfParallelism = 2
        };
        var settings = new JobbyServerSettings
        {
            Queues = [queueSettings],
        };
        var queueService = CreateQueueService(settings);
        
        await queueService.TakeBatchToProcessing(batchSize, new List<JobExecutionModel>());

        _storageMock.VerifyTakeToProcessing(ServerId, queueSettings.MaxDegreeOfParallelism, queueSettings.QueueName);
    }

    private SingleQueueService CreateQueueService(JobbyServerSettings settings)
    {
        return new SingleQueueService(_storageMock.Object, _timerMock.Object, settings, ServerId);
    }
}