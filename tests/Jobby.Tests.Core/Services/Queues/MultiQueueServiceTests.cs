using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services.Queues;
using Jobby.TestsUtils.Mocks;
using Moq;

namespace Jobby.Tests.Core.Services.Queues;

public class MultiQueueServiceTests
{
    private const string ServerId = "ServerId";
    
    private readonly Mock<IJobbyStorage> _storageMock = new();
    private readonly Mock<ITimerService> _timerMock = new();

    [Fact]
    public async Task TakeBatchToProcessing_FirstCall_TakesFromFirst()
    {
        var setting = GetSettings();
        var queueService = CreateQueueService(setting);

        await queueService.TakeBatchToProcessing(setting.MaxDegreeOfParallelism, new List<JobExecutionModel>());
        
        _storageMock.VerifyTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[0].QueueName);
    }

    [Fact]
    public async Task TakeBatchToProcessing_SecondCall_FirstQueueNotExhausted_TakesFromFirst()
    {
        var setting = GetSettings();
        var batchSize = 2; // less than setting.MaxParallelismDegree
        var jobs = new List<JobExecutionModel>
        {
            new(), new()
        };
        _storageMock.SetupTakeToProcessing(ServerId, batchSize, setting.Queues[0].QueueName, jobs);
        
        var queueService = CreateQueueService(setting);

        await queueService.TakeBatchToProcessing(batchSize, new List<JobExecutionModel>());
        await queueService.TakeBatchToProcessing(batchSize, new List<JobExecutionModel>());
        
        _storageMock.VerifyTakeToProcessing(ServerId, batchSize, setting.Queues[0].QueueName, Times.Exactly(2));
    }

    [Fact]
    public async Task TakeBatchToProcessing_SecondCall_FirstQueueCompleted_TakesFromSecond()
    {
        var setting = GetSettings();
        var jobs = new List<JobExecutionModel>
        {
            new(), new(), new(), new(), new()
        };
        _storageMock.SetupTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[0].QueueName, jobs);

        var queueService = CreateQueueService(setting);

        await queueService.TakeBatchToProcessing(setting.MaxDegreeOfParallelism, new List<JobExecutionModel>());
        await queueService.TakeBatchToProcessing(setting.MaxDegreeOfParallelism, new List<JobExecutionModel>());
        
        _storageMock.VerifyTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[0].QueueName);
        _storageMock.VerifyTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[1].QueueName);
    }
    
    [Fact]
    public async Task TakeBatchToProcessing_SecondCall_FirstQueueReturnedLessThanRequested_TakesFromSecond()
    {
        var setting = GetSettings();
        var jobs = new List<JobExecutionModel>
        {
            new(), new()
        };
        _storageMock.SetupTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[0].QueueName, jobs);

        var queueService = CreateQueueService(setting);

        await queueService.TakeBatchToProcessing(setting.MaxDegreeOfParallelism, new List<JobExecutionModel>());
        await queueService.TakeBatchToProcessing(setting.MaxDegreeOfParallelism, new List<JobExecutionModel>());
        
        _storageMock.VerifyTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[0].QueueName);
        _storageMock.VerifyTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[1].QueueName);
    }
    
    [Fact]
    public async Task TakeBatchToProcessing_SecondCall_FirstQueueEmpty_TakesFromSecond()
    {
        var setting = GetSettings();
        var jobs = new List<JobExecutionModel>();
        _storageMock.SetupTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[0].QueueName, jobs);

        var queueService = CreateQueueService(setting);

        await queueService.TakeBatchToProcessing(setting.MaxDegreeOfParallelism, new List<JobExecutionModel>());
        await queueService.TakeBatchToProcessing(setting.MaxDegreeOfParallelism, new List<JobExecutionModel>());
        
        _storageMock.VerifyTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[0].QueueName);
        _storageMock.VerifyTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[1].QueueName);
    }

    [Fact]
    public async Task TakeBatchToProcessing_ThirdCall_FirstQueueCompletedByTwoRequests_TakesFromSecond()
    {
        var setting = GetSettings();

        var queueService = CreateQueueService(setting);

        var firstResult = new List<JobExecutionModel> { new(), new() };
        _storageMock.SetupTakeToProcessing(ServerId, 2, setting.Queues[0].QueueName, firstResult);
        await queueService.TakeBatchToProcessing(2, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(ServerId, 2, setting.Queues[0].QueueName);

        var secondResult = new List<JobExecutionModel> { new(), new(), new() };
        _storageMock.SetupTakeToProcessing(ServerId, 3, setting.Queues[0].QueueName, secondResult);
        await queueService.TakeBatchToProcessing(3, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(ServerId, 3, setting.Queues[0].QueueName);

        await queueService.TakeBatchToProcessing(setting.MaxDegreeOfParallelism, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[1].QueueName);
    }

    [Fact]
    public async Task TakeBatchToProcessing_ThirdCall_FirstEmptySecondNotEmpty_WaitingIntervalForFirstNotExpired_TakesFromSecond()
    {
        var setting = GetSettings();

        var queueService = CreateQueueService(setting);

        var firstResult = new List<JobExecutionModel>();
        _storageMock.SetupTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[0].QueueName, firstResult);
        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(1);
        await queueService.TakeBatchToProcessing(setting.MaxDegreeOfParallelism, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[0].QueueName);
        
        var secondResult = new List<JobExecutionModel> { new(), new(), new(), new(), new() };
        _storageMock.SetupTakeToProcessing(ServerId, 5, setting.Queues[1].QueueName, secondResult);
        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(2);
        await queueService.TakeBatchToProcessing(5, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(ServerId, 5, setting.Queues[1].QueueName);
        
        // less than polling interval
        _timerMock.Setup(x => x.GetElapsedTime(1)).Returns(TimeSpan.FromMilliseconds(5)); 
        _timerMock.Setup(x => x.GetElapsedTime(2)).Returns(TimeSpan.FromMilliseconds(2));
        await queueService.TakeBatchToProcessing(3, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(ServerId, 3, setting.Queues[1].QueueName);
    }
    
    [Fact]
    public async Task TakeBatchToProcessing_ThirdCall_FirstEmptySecondNotEmpty_WaitingIntervalForFirstExpired_TakesFromFirst()
    {
        var setting = GetSettings();

        var queueService = CreateQueueService(setting);

        var firstResult = new List<JobExecutionModel>();
        _storageMock.SetupTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[0].QueueName, firstResult);
        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(1);
        await queueService.TakeBatchToProcessing(setting.MaxDegreeOfParallelism, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[0].QueueName);
        
        var secondResult = new List<JobExecutionModel> { new(), new(), new(), new(), new() };
        _storageMock.SetupTakeToProcessing(ServerId, 5, setting.Queues[1].QueueName, secondResult);
        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(2);
        await queueService.TakeBatchToProcessing(5, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(ServerId, 5, setting.Queues[1].QueueName);
        
        // more than polling interval
        _timerMock.Setup(x => x.GetElapsedTime(1)).Returns(TimeSpan.FromMilliseconds(5000)); 
        _timerMock.Setup(x => x.GetElapsedTime(2)).Returns(TimeSpan.FromMilliseconds(2));
        await queueService.TakeBatchToProcessing(3, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(ServerId, 3, setting.Queues[0].QueueName);
    }    
    
    [Fact]
    public async Task TakeBatchToProcessing_ThirdCall_FirstAndSecondCompleted_TakesFromFirst()
    {
        var setting = GetSettings(firstParallelismDegree: 2, secondParallelismDegree: 3);

        var queueService = CreateQueueService(setting);

        var firstResult = new List<JobExecutionModel> { new(), new() };
        _storageMock.SetupTakeToProcessing(ServerId, 2, setting.Queues[0].QueueName, firstResult);
        await queueService.TakeBatchToProcessing(2, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(ServerId, 2, setting.Queues[0].QueueName);

        var secondResult = new List<JobExecutionModel> { new(), new(), new() };
        _storageMock.SetupTakeToProcessing(ServerId, 3, setting.Queues[1].QueueName, secondResult);
        await queueService.TakeBatchToProcessing(3, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(ServerId, 3, setting.Queues[1].QueueName);

        await queueService.TakeBatchToProcessing(1, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(ServerId, 1, setting.Queues[0].QueueName);
    }
    
    [Fact]
    public async Task TakeBatchToProcessing_ThirdCall_FirstAndSecondEmpty_TakesFromFirst()
    {
        var setting = GetSettings();

        var queueService = CreateQueueService(setting);

        var firstResult = new List<JobExecutionModel>();
        _storageMock.SetupTakeToProcessing(ServerId, 2, setting.Queues[0].QueueName, firstResult);
        await queueService.TakeBatchToProcessing(2, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(ServerId, 2, setting.Queues[0].QueueName);

        var secondResult = new List<JobExecutionModel>();
        _storageMock.SetupTakeToProcessing(ServerId, 3, setting.Queues[1].QueueName, secondResult);
        await queueService.TakeBatchToProcessing(3, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(ServerId, 3, setting.Queues[1].QueueName);

        await queueService.TakeBatchToProcessing(1, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(ServerId, 1, setting.Queues[0].QueueName);
    }

    [Fact]
    public async Task
        TakeBatchToProcessing_RequestedMoreThanMaxForQueue_TakesWithQueueMaxBatchSizeInsteadOfRequested()
    {
        var setting = GetSettings(firstParallelismDegree: 1);
        var queueService = CreateQueueService(setting);

        await queueService.TakeBatchToProcessing(setting.MaxDegreeOfParallelism, new List<JobExecutionModel>());
        
        _storageMock.VerifyTakeToProcessing(ServerId, 1, setting.Queues[0].QueueName);
    }

    [Fact]
    public void WaitIfEmpty_FirstCall_DoesNotDelay()
    {
        var setting = GetSettings();
        var queueService = CreateQueueService(setting);
        
        var waitingTask = queueService.WaitIfEmpty();
        
        Assert.Equal(Task.CompletedTask, waitingTask);
        _timerMock.Verify(x => x.Delay(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task WaitIfEmpty_FirstCompletedSecondNotRequestedYet_DoesNotDelay()
    {
        var setting = GetSettings();
        var jobs = new List<JobExecutionModel>
        {
            new(), new(), new(), new(), new()
        };
        _storageMock.SetupTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[0].QueueName, jobs);

        var queueService = CreateQueueService(setting);

        await queueService.TakeBatchToProcessing(setting.MaxDegreeOfParallelism, new List<JobExecutionModel>());
        
        var waitingTask = queueService.WaitIfEmpty();
        
        Assert.Equal(Task.CompletedTask, waitingTask);
        _timerMock.Verify(x => x.Delay(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task WaitIfEmpty_QueuesEmpty_PollingIntervalExpired_DoesNotDelay()
    {
        var setting = GetSettings();
        var jobs = new List<JobExecutionModel>();
        _storageMock.SetupTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[0].QueueName, jobs);
        _storageMock.SetupTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[1].QueueName, jobs);

        var queueService = CreateQueueService(setting);

        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(1);
        await queueService.TakeBatchToProcessing(setting.MaxDegreeOfParallelism, new List<JobExecutionModel>());
        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(2);
        await queueService.TakeBatchToProcessing(setting.MaxDegreeOfParallelism, new List<JobExecutionModel>());
        
        _timerMock.Setup(x => x.GetElapsedTime(1)).Returns(TimeSpan.FromMilliseconds(5000));
        var waitingTask = queueService.WaitIfEmpty();
        
        Assert.Equal(Task.CompletedTask, waitingTask);
        _timerMock.Verify(x => x.Delay(It.IsAny<int>()), Times.Never);
    }
    
    [Fact]
    public async Task WaitIfEmpty_QueuesEmpty_PollingIntervalNotExpired_DoesDelay()
    {
        var setting = GetSettings();
        var jobs = new List<JobExecutionModel>();
        _storageMock.SetupTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[0].QueueName, jobs);
        _storageMock.SetupTakeToProcessing(ServerId, setting.MaxDegreeOfParallelism, setting.Queues[1].QueueName, jobs);

        var queueService = CreateQueueService(setting);

        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(1);
        await queueService.TakeBatchToProcessing(setting.MaxDegreeOfParallelism, new List<JobExecutionModel>());
        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(2);
        await queueService.TakeBatchToProcessing(setting.MaxDegreeOfParallelism, new List<JobExecutionModel>());
        
        _timerMock.Setup(x => x.GetElapsedTime(1)).Returns(TimeSpan.FromMilliseconds(100));
        var waitingTask = queueService.WaitIfEmpty();
        
        Assert.NotEqual(Task.CompletedTask, waitingTask);
        _timerMock.Verify(x => x.Delay(900), Times.Once);
    }    

    private JobbyServerSettings GetSettings(int firstParallelismDegree = 0, int secondParallelismDegree = 0)
    {
        return new JobbyServerSettings
        {
            MaxDegreeOfParallelism = 5,
            Queues =
            [
                new QueueSettings { QueueName = "q1", MaxDegreeOfParallelism = firstParallelismDegree },
                new QueueSettings { QueueName = "q2", MaxDegreeOfParallelism = secondParallelismDegree },
            ]
        };
    }

    private MultiQueueService CreateQueueService(JobbyServerSettings settings)
    {
        return new MultiQueueService(_storageMock.Object, _timerMock.Object, settings, ServerId);
    }
}