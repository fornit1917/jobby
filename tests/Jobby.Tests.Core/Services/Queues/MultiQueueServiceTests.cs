using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Queues;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.Core.Services.Queues;
using Jobby.Core.Services.ServerModules.JobsExecution;
using Jobby.TestsUtils.Mocks;
using Moq;

namespace Jobby.Tests.Core.Services.Queues;

public class MultiQueueServiceTests
{
    private const string ServerId = "ServerId";
    
    private readonly Mock<IJobbyStorage> _storageMock = new();
    private readonly Mock<ITimerService> _timerMock = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TakeBatchToProcessing_FirstCall_TakesFromFirst(bool disableSerializableGroup)
    {
        var config = new QueueServiceConfig
        {
            WaitingIntervalStartMs = 1000,
            WaitingIntervalMaxMs = 1000,
            WaitingIntervalFactor = 1,
            Queues = [
                new()  { QueueName = "q1", MaxBatchSize = 10, DisableSerializableGroups = disableSerializableGroup },
                new()  { QueueName = "q2", MaxBatchSize = 10 },
            ]
        };
        var queueService = CreateQueueService(config);

        await queueService.ReadBatch(10, new List<JobExecutionModel>());

        var expectedRequest = new GetJobsRequest
        {
            QueueName = config.Queues[0].QueueName,
            BatchSize = 10,
            ServerId = ServerId,
            DisableSerializableGroups = disableSerializableGroup
        };
        _storageMock.VerifyTakeToProcessing(expectedRequest);
    }

    [Fact]
    public async Task TakeBatchToProcessing_SecondCall_FirstQueueNotExhausted_TakesFromFirst()
    {
        var maxBatchSize = 10;
        var config = GetConfig(maxBatchSize, maxBatchSize);
        var batchSize = 2;
        var expectedRequest = new GetJobsRequest
        {
            QueueName = config.Queues[0].QueueName,
            BatchSize = batchSize,
            ServerId = ServerId
        };
        var jobs = new List<JobExecutionModel>
        {
            new(), new()
        };
        _storageMock.SetupTakeToProcessing(expectedRequest, jobs);
        
        var queueService = CreateQueueService(config);

        await queueService.ReadBatch(batchSize, new List<JobExecutionModel>());
        await queueService.ReadBatch(batchSize, new List<JobExecutionModel>());
        
        _storageMock.VerifyTakeToProcessing(expectedRequest, Times.Exactly(2));
    }

    [Fact]
    public async Task TakeBatchToProcessing_SecondCall_FirstQueueCompleted_TakesFromSecond()
    {
        var maxBatchSize = 10;
        var config = GetConfig(maxBatchSize, maxBatchSize);
        var firstRequest = new GetJobsRequest
        {
            QueueName = config.Queues[0].QueueName,
            BatchSize = maxBatchSize,
            ServerId = ServerId
        };
        var jobs = new List<JobExecutionModel>
        {
            new(), new(), new(), new(), new()
        };
        _storageMock.SetupTakeToProcessing(firstRequest, jobs);

        var queueService = CreateQueueService(config);

        await queueService.ReadBatch(maxBatchSize, new List<JobExecutionModel>());
        await queueService.ReadBatch(maxBatchSize, new List<JobExecutionModel>());
        
        _storageMock.VerifyTakeToProcessing(firstRequest);
        var secondRequest = new GetJobsRequest
        {
            QueueName = config.Queues[1].QueueName,
            BatchSize = maxBatchSize,
            ServerId = ServerId
        };
        _storageMock.VerifyTakeToProcessing(secondRequest);
    }
    
    [Fact]
    public async Task TakeBatchToProcessing_SecondCall_FirstQueueReturnedLessThanRequested_TakesFromSecond()
    {
        var maxBatchSize = 10;
        var config = GetConfig(maxBatchSize, maxBatchSize);
        var firstRequest = new GetJobsRequest
        {
            QueueName = config.Queues[0].QueueName,
            BatchSize = maxBatchSize,
            ServerId = ServerId
        };
        var jobs = new List<JobExecutionModel>
        {
            new(), new()
        };
        _storageMock.SetupTakeToProcessing(firstRequest, jobs);

        var queueService = CreateQueueService(config);

        await queueService.ReadBatch(maxBatchSize, new List<JobExecutionModel>());
        await queueService.ReadBatch(maxBatchSize, new List<JobExecutionModel>());
        
        _storageMock.VerifyTakeToProcessing(firstRequest);
        var secondRequest = new GetJobsRequest
        {
            QueueName = config.Queues[1].QueueName,
            BatchSize = maxBatchSize,
            ServerId = ServerId
        };
        _storageMock.VerifyTakeToProcessing(secondRequest);
    }
    
    [Fact]
    public async Task TakeBatchToProcessing_SecondCall_FirstQueueEmpty_TakesFromSecond()
    {
        var maxBatchSize = 10;
        var config = GetConfig(maxBatchSize, maxBatchSize);
        var firstRequest = new GetJobsRequest
        {
            QueueName = config.Queues[0].QueueName,
            BatchSize = maxBatchSize,
            ServerId = ServerId
        };
        var jobs = new List<JobExecutionModel>();
        _storageMock.SetupTakeToProcessing(firstRequest, jobs);

        var queueService = CreateQueueService(config);

        await queueService.ReadBatch(maxBatchSize, new List<JobExecutionModel>());
        await queueService.ReadBatch(maxBatchSize, new List<JobExecutionModel>());
        
        _storageMock.VerifyTakeToProcessing(firstRequest);
        var secondRequest = new GetJobsRequest
        {
            QueueName = config.Queues[1].QueueName,
            BatchSize = maxBatchSize,
            ServerId = ServerId
        };
        _storageMock.VerifyTakeToProcessing(secondRequest);
    }

    [Fact]
    public async Task TakeBatchToProcessing_ThirdCall_FirstQueueCompletedByTwoRequests_TakesFromSecond()
    {
        int maxBatchSize = 5;
        var config = GetConfig(maxBatchSize, maxBatchSize);

        var queueService = CreateQueueService(config);

        var firstRequest = new GetJobsRequest
        {
            QueueName = config.Queues[0].QueueName,
            BatchSize = 2,
            ServerId = ServerId
        };
        var firstResult = new List<JobExecutionModel> { new(), new() };
        _storageMock.SetupTakeToProcessing(firstRequest, firstResult);
        await queueService.ReadBatch(2, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(firstRequest);

        var secondRequest = new GetJobsRequest
        {
            QueueName = config.Queues[0].QueueName,
            BatchSize = 3,
            ServerId = ServerId
        };
        var secondResult = new List<JobExecutionModel> { new(), new(), new() };
        _storageMock.SetupTakeToProcessing(secondRequest, secondResult);
        await queueService.ReadBatch(3, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(secondRequest);

        await queueService.ReadBatch(maxBatchSize, new List<JobExecutionModel>());
        var thirdRequest = new GetJobsRequest
        {
            QueueName = config.Queues[1].QueueName,
            BatchSize = maxBatchSize,
            ServerId = ServerId
        };
        _storageMock.VerifyTakeToProcessing(thirdRequest);
    }

    [Fact]
    public async Task TakeBatchToProcessing_ThirdCall_FirstEmptySecondNotEmpty_WaitingIntervalForFirstNotExpired_TakesFromSecond()
    {
        int maxBatchSize = 5;
        var config = GetConfig(maxBatchSize, maxBatchSize);

        var queueService = CreateQueueService(config);

        var firstRequest = new GetJobsRequest
        {
            QueueName = config.Queues[0].QueueName,
            BatchSize = maxBatchSize,
            ServerId = ServerId
        };
        var firstResult = new List<JobExecutionModel>();
        _storageMock.SetupTakeToProcessing(firstRequest, firstResult);
        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(1);
        await queueService.ReadBatch(maxBatchSize, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(firstRequest);
        
        var secondRequest = new GetJobsRequest
        {
            QueueName = config.Queues[1].QueueName,
            BatchSize = 5,
            ServerId = ServerId
        };
        var secondResult = new List<JobExecutionModel> { new(), new(), new(), new(), new() };
        _storageMock.SetupTakeToProcessing(secondRequest, secondResult);
        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(2);
        await queueService.ReadBatch(5, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(secondRequest);
        
        // less than polling interval
        _timerMock.Setup(x => x.GetElapsedTime(1)).Returns(TimeSpan.FromMilliseconds(5)); 
        _timerMock.Setup(x => x.GetElapsedTime(2)).Returns(TimeSpan.FromMilliseconds(2));
        await queueService.ReadBatch(3, new List<JobExecutionModel>());
        var thirdRequest = new GetJobsRequest
        {
            QueueName = config.Queues[1].QueueName,
            BatchSize = 3,
            ServerId = ServerId
        };
        _storageMock.VerifyTakeToProcessing(thirdRequest);
    }
    
    [Fact]
    public async Task TakeBatchToProcessing_ThirdCall_FirstEmptySecondNotEmpty_WaitingIntervalForFirstExpired_TakesFromFirst()
    {
        var maxBatchSize = 5;
        var config = GetConfig(maxBatchSize, maxBatchSize);

        var queueService = CreateQueueService(config);

        var firstRequest = new GetJobsRequest
        {
            QueueName = config.Queues[0].QueueName,
            BatchSize = maxBatchSize,
            ServerId = ServerId
        };
        var firstResult = new List<JobExecutionModel>();
        _storageMock.SetupTakeToProcessing(firstRequest, firstResult);
        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(1);
        await queueService.ReadBatch(maxBatchSize, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(firstRequest);
        
        var secondRequest = new GetJobsRequest
        {
            QueueName = config.Queues[1].QueueName,
            BatchSize = 5,
            ServerId = ServerId
        };
        var secondResult = new List<JobExecutionModel> { new(), new(), new(), new(), new() };
        _storageMock.SetupTakeToProcessing(secondRequest, secondResult);
        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(2);
        await queueService.ReadBatch(5, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(secondRequest);
        
        // more than polling interval
        _timerMock.Setup(x => x.GetElapsedTime(1)).Returns(TimeSpan.FromMilliseconds(5000)); 
        _timerMock.Setup(x => x.GetElapsedTime(2)).Returns(TimeSpan.FromMilliseconds(2));
        await queueService.ReadBatch(3, new List<JobExecutionModel>());
        var thirdRequest = new GetJobsRequest
        {
            QueueName = config.Queues[0].QueueName,
            BatchSize = 3,
            ServerId = ServerId
        };
        _storageMock.VerifyTakeToProcessing(thirdRequest);
    }    
    
    [Fact]
    public async Task TakeBatchToProcessing_ThirdCall_FirstAndSecondCompleted_TakesFromFirst()
    {
        var config = GetConfig(firstMaxBatchSize: 2, secondMaxBatchSize: 3);

        var queueService = CreateQueueService(config);

        var firstRequest = new GetJobsRequest
        {
            QueueName = config.Queues[0].QueueName,
            BatchSize = 2,
            ServerId = ServerId
        };
        var firstResult = new List<JobExecutionModel> { new(), new() };
        _storageMock.SetupTakeToProcessing(firstRequest, firstResult);
        await queueService.ReadBatch(2, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(firstRequest);

        var secondRequest = new GetJobsRequest
        {
            QueueName = config.Queues[1].QueueName,
            BatchSize = 3,
            ServerId = ServerId
        };
        var secondResult = new List<JobExecutionModel> { new(), new(), new() };
        _storageMock.SetupTakeToProcessing(secondRequest, secondResult);
        await queueService.ReadBatch(3, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(secondRequest);

        await queueService.ReadBatch(1, new List<JobExecutionModel>());
        var thirdRequest = new GetJobsRequest
        {
            QueueName = config.Queues[0].QueueName,
            BatchSize = 1,
            ServerId = ServerId
        };
        _storageMock.VerifyTakeToProcessing(thirdRequest);
    }
    
    [Fact]
    public async Task TakeBatchToProcessing_ThirdCall_FirstAndSecondEmpty_TakesFromFirst()
    {
        var config = GetConfig();

        var queueService = CreateQueueService(config);

        var firstRequest = new GetJobsRequest
        {
            QueueName = config.Queues[0].QueueName,
            BatchSize = 2,
            ServerId = ServerId
        };
        var firstResult = new List<JobExecutionModel>();
        _storageMock.SetupTakeToProcessing(firstRequest, firstResult);
        await queueService.ReadBatch(2, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(firstRequest);

        var secondRequest = new GetJobsRequest
        {
            QueueName = config.Queues[1].QueueName,
            BatchSize = 3,
            ServerId = ServerId
        };
        var secondResult = new List<JobExecutionModel>();
        _storageMock.SetupTakeToProcessing(secondRequest, secondResult);
        await queueService.ReadBatch(3, new List<JobExecutionModel>());
        _storageMock.VerifyTakeToProcessing(secondRequest);

        await queueService.ReadBatch(1, new List<JobExecutionModel>());
        var thirdRequest = new GetJobsRequest
        {
            QueueName = config.Queues[0].QueueName,
            BatchSize = 1,
            ServerId = ServerId
        };
        _storageMock.VerifyTakeToProcessing(thirdRequest);
    }

    [Fact]
    public async Task
        TakeBatchToProcessing_RequestedMoreThanMaxForQueue_TakesWithQueueMaxBatchSizeInsteadOfRequested()
    {
        var config = GetConfig(firstMaxBatchSize: 1);
        var queueService = CreateQueueService(config);

        await queueService.ReadBatch(5, new List<JobExecutionModel>());

        var expectedRequest = new GetJobsRequest
        {
            QueueName = config.Queues[0].QueueName,
            BatchSize = 1,
            ServerId = ServerId
        };
        _storageMock.VerifyTakeToProcessing(expectedRequest);
    }

    [Fact]
    public void GetWaitingIntervalMs_FirstCall_ReturnsZero()
    {
        var config = GetConfig();
        var queueService = CreateQueueService(config);
        
        var intervalMs = queueService.GetWaitingIntervalMs();
        
        Assert.Equal(0, intervalMs);
    }

    [Fact]
    public async Task GetWaitingIntervalMs_FirstCompletedSecondNotRequestedYet_ReturnsZero()
    {
        int maxBatchSize = 5;
        var config = GetConfig(maxBatchSize, maxBatchSize);
        var request = new GetJobsRequest
        {
            QueueName = config.Queues[0].QueueName,
            BatchSize = maxBatchSize,
            ServerId = ServerId
        };
        var jobs = new List<JobExecutionModel>
        {
            new(), new(), new(), new(), new()
        };
        _storageMock.SetupTakeToProcessing(request, jobs);

        var queueService = CreateQueueService(config);

        await queueService.ReadBatch(maxBatchSize, new List<JobExecutionModel>());
        
        var intervalMs = queueService.GetWaitingIntervalMs();
        
        Assert.Equal(0, intervalMs);
    }

    [Fact]
    public async Task GetWaitingIntervalMs_QueuesEmpty_PollingIntervalExpired_ReturnsZero()
    {
        int maxBatchSize = 5;
        var config = GetConfig(maxBatchSize, maxBatchSize);
        var jobs = new List<JobExecutionModel>();
        var firstRequest = new GetJobsRequest
        {
            QueueName = config.Queues[0].QueueName,
            BatchSize = maxBatchSize,
            ServerId = ServerId
        };
        var secondRequest = new GetJobsRequest
        {
            QueueName = config.Queues[1].QueueName,
            BatchSize = maxBatchSize,
            ServerId = ServerId
        };
        _storageMock.SetupTakeToProcessing(firstRequest, jobs);
        _storageMock.SetupTakeToProcessing(secondRequest, jobs);

        var queueService = CreateQueueService(config);

        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(1);
        await queueService.ReadBatch(maxBatchSize, new List<JobExecutionModel>());
        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(2);
        await queueService.ReadBatch(maxBatchSize, new List<JobExecutionModel>());
        
        _timerMock.Setup(x => x.GetElapsedTime(1)).Returns(TimeSpan.FromMilliseconds(5000));
        _timerMock.Setup(x => x.GetElapsedTime(2)).Returns(TimeSpan.FromMilliseconds(5000));
        var intervalMs = queueService.GetWaitingIntervalMs();
        
        Assert.Equal(0, intervalMs);
    }
    
    [Fact]
    public async Task GetWaitingIntervalMs_QueuesEmpty_PollingIntervalNotExpired_ReturnsWaitingInterval()
    {
        int maxBatchSize = 5;
        var config = GetConfig(maxBatchSize, maxBatchSize);
        var jobs = new List<JobExecutionModel>();
        var firstRequest = new GetJobsRequest
        {
            QueueName = config.Queues[0].QueueName,
            BatchSize = maxBatchSize,
            ServerId = ServerId
        };
        var secondRequest = new GetJobsRequest
        {
            QueueName = config.Queues[1].QueueName,
            BatchSize = maxBatchSize,
            ServerId = ServerId
        };
        _storageMock.SetupTakeToProcessing(firstRequest, jobs);
        _storageMock.SetupTakeToProcessing(secondRequest, jobs);

        var queueService = CreateQueueService(config);

        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(1);
        await queueService.ReadBatch(maxBatchSize, new List<JobExecutionModel>());
        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(2);
        await queueService.ReadBatch(maxBatchSize, new List<JobExecutionModel>());
        
        _timerMock.Setup(x => x.GetElapsedTime(1)).Returns(TimeSpan.FromMilliseconds(100));
        var intervalMs = queueService.GetWaitingIntervalMs();
        
        Assert.Equal(config.WaitingIntervalMaxMs - 100, intervalMs);
    }    

    [Fact]
    public async Task TakeBatchToProcessing_ColdQueueShortRead_MovesBackToHotList()
    {
        var config = GetConfig();
        var q1Request = new GetJobsRequest { QueueName = "q1", BatchSize = 5, ServerId = ServerId };
        var q2Request = new GetJobsRequest { QueueName = "q2", BatchSize = 5, ServerId = ServerId };
        var requestCount = 0;
        _storageMock
            .Setup(x => x.TakeBatchToProcessingAsync(It.IsAny<GetJobsRequest>(), It.IsAny<List<JobExecutionModel>>()))
            .Callback<GetJobsRequest, List<JobExecutionModel>>((req, output) =>
            {
                output.Clear();
                if (req.QueueName == "q1")
                {
                    // first read - empty (goes to cold list), second read - short read
                    if (requestCount == 0)
                    {
                        output.AddRange([]);
                    }
                    else
                    {
                        output.AddRange([new(), new()]);
                    }
                }
                else
                {
                    // full read
                    output.AddRange(Enumerable.Range(0, 5).Select(_ => new JobExecutionModel()));
                }
                requestCount++;
            })
            .Returns(Task.CompletedTask);

        var queueService = CreateQueueService(config);

        // q1 empty -> cold list
        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(1);
        await queueService.ReadBatch(5, new List<JobExecutionModel>());

        // q2 full -> completed, rotation continues
        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(2);
        await queueService.ReadBatch(5, new List<JobExecutionModel>());

        // q1 chosen from cold list (waiting interval expired)
        _timerMock.Setup(x => x.GetElapsedTime(1)).Returns(TimeSpan.FromMilliseconds(5000));
        await queueService.ReadBatch(5, new List<JobExecutionModel>());

        // short read must return q1 back to the hot list, so q2 is next
        await queueService.ReadBatch(5, new List<JobExecutionModel>());

        _storageMock.VerifyTakeToProcessing(q1Request, Times.Exactly(2));
        _storageMock.VerifyTakeToProcessing(q2Request, Times.Exactly(2));
    }

    [Fact]
    public async Task TakeBatchToProcessing_ThreeQueues_RotatesThroughAllQueues()
    {
        var config = new QueueServiceConfig
        {
            WaitingIntervalStartMs = 1000,
            WaitingIntervalFactor = 1,
            WaitingIntervalMaxMs = 1000,
            Queues =
            [
                new() { QueueName = "q1", MaxBatchSize = 1 },
                new() { QueueName = "q2", MaxBatchSize = 1 },
                new() { QueueName = "q3", MaxBatchSize = 1 }
            ]
        };
        _storageMock
            .Setup(x => x.TakeBatchToProcessingAsync(It.IsAny<GetJobsRequest>(), It.IsAny<List<JobExecutionModel>>()))
            .Callback<GetJobsRequest, List<JobExecutionModel>>((req, output) =>
            {
                output.Clear();
                // q2 is empty, the rest return full batch of 1
                if (req.QueueName != "q2")
                {
                    output.Add(new JobExecutionModel());
                }
            })
            .Returns(Task.CompletedTask);

        var queueService = CreateQueueService(config);

        await queueService.ReadBatch(1, new List<JobExecutionModel>());
        await queueService.ReadBatch(1, new List<JobExecutionModel>());
        await queueService.ReadBatch(1, new List<JobExecutionModel>());
        await queueService.ReadBatch(1, new List<JobExecutionModel>());

        var q1Request = new GetJobsRequest { QueueName = "q1", BatchSize = 1, ServerId = ServerId };
        var q2Request = new GetJobsRequest { QueueName = "q2", BatchSize = 1, ServerId = ServerId };
        var q3Request = new GetJobsRequest { QueueName = "q3", BatchSize = 1, ServerId = ServerId };
        _storageMock.VerifyTakeToProcessing(q1Request, Times.Exactly(2));
        _storageMock.VerifyTakeToProcessing(q2Request, Times.Exactly(1));
        _storageMock.VerifyTakeToProcessing(q3Request, Times.Exactly(1));
    }

    [Fact]
    public async Task GetWaitingIntervalMs_EachCallConsumesNextPollingIntervalValue()
    {
        // Pins current behaviour: both GetWaitingIntervalMs and ChooseCurrentQueueFromWaitingList
        // call _pollingInterval.GetCurrentValueAndSetToNext(), so every waiting-interval
        // call consumes one step of the geometric progression
        var config = new QueueServiceConfig
        {
            WaitingIntervalStartMs = 1000,
            WaitingIntervalFactor = 2,
            WaitingIntervalMaxMs = 8000,
            Queues =
            [
                new() { QueueName = "q1", MaxBatchSize = 5 },
                new() { QueueName = "q2", MaxBatchSize = 5 }
            ]
        };
        var firstRequest = new GetJobsRequest { QueueName = "q1", BatchSize = 5, ServerId = ServerId };
        var secondRequest = new GetJobsRequest { QueueName = "q2", BatchSize = 5, ServerId = ServerId };
        _storageMock.SetupTakeToProcessing(firstRequest, []);
        _storageMock.SetupTakeToProcessing(secondRequest, []);

        var queueService = CreateQueueService(config);

        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(1);
        await queueService.ReadBatch(5, new List<JobExecutionModel>());
        _timerMock.Setup(x => x.GetCurrentTicks()).Returns(2);
        await queueService.ReadBatch(5, new List<JobExecutionModel>());

        _timerMock.Setup(x => x.GetElapsedTime(1)).Returns(TimeSpan.FromMilliseconds(100));

        // first call: current progression value 1000 -> delay = 1000 - 100
        var interval1 = queueService.GetWaitingIntervalMs();
        // second call: progression already advanced to 2000 -> delay = 2000 - 100
        var interval2 = queueService.GetWaitingIntervalMs();

        Assert.Equal(900, interval1);
        Assert.Equal(1900, interval2);
    }

    private QueueServiceConfig GetConfig(int firstMaxBatchSize = 5, int secondMaxBatchSize = 5)
    {
        return new QueueServiceConfig
        {
            WaitingIntervalStartMs = 1000,
            WaitingIntervalFactor = 1,
            WaitingIntervalMaxMs = 1000,
            Queues =
            [
                new() { QueueName = "q1", MaxBatchSize = firstMaxBatchSize },
                new() { QueueName = "q2", MaxBatchSize = secondMaxBatchSize }
            ]
        };
    }

    private MultiQueueService<JobExecutionModel> CreateQueueService(QueueServiceConfig config)
    {
        var reader = new TakeToProcessingJobsQueueReader(_storageMock.Object);
        return new MultiQueueService<JobExecutionModel>(reader, _timerMock.Object, config, ServerId);
    }
}