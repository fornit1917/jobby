using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Queues;
using Jobby.Core.Models;
using Jobby.Core.Services.Queues;
using Jobby.Core.Services.ServerModules.JobsExecution;
using Jobby.TestsUtils.Mocks;
using Moq;

namespace Jobby.Tests.Core.Services.Queues;

public class SingleQueueServiceTests
{
    private const string ServerId = "ServerId";
    private readonly Mock<IJobbyStorage> _storageMock = new();
    private readonly Mock<ITimerService> _timerMock = new();

    [Fact]
    public void GetWaitingIntervalMs_UnknownQueueState_ReturnsZero()
    {
        var config = GetQueueServiceConfig();
        var queueService = CreateQueueService(config);
        
        var intervalMs = queueService.GetWaitingIntervalMs();
        
        Assert.Equal(0, intervalMs);
    }

    [Fact]
    public async Task GetWaitingIntervalMs_NotEmptyQueue_ReturnsZero()
    {
        var batchSize = 5;
        var request = new GetJobsRequest
        {
            QueueName = QueueSettings.DefaultQueueName,
            BatchSize = batchSize,
            ServerId = ServerId,
        };        
        var jobs = new List<JobExecutionModel>()
        {
            new()
        };
        _storageMock.SetupTakeToProcessing(request, jobs);

        var config = GetQueueServiceConfig();
        var queueService = CreateQueueService(config);

        await queueService.ReadBatch(batchSize, new List<JobExecutionModel>());
        var intervalMs = queueService.GetWaitingIntervalMs();
        
        Assert.Equal(0, intervalMs);
    }
    
    [Fact]
    public async Task GetWaitingIntervalMs_EmptyQueue_ReturnsWaitingInterval()
    {
        var batchSize = 5;
        var request = new GetJobsRequest
        {
            QueueName = QueueSettings.DefaultQueueName,
            BatchSize = batchSize,
            ServerId = ServerId,
        };
        var jobs = new List<JobExecutionModel>();
        _storageMock.SetupTakeToProcessing(request, jobs);

        var config = GetQueueServiceConfig();
        var queueService = CreateQueueService(config);

        await queueService.ReadBatch(batchSize, new List<JobExecutionModel>());
        var intervalMs =  queueService.GetWaitingIntervalMs();
        
        Assert.Equal(config.WaitingIntervalStartMs, intervalMs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TakeBatchToProcessing_TakesJobsBySpecifiedQueueConfig(bool disableSerializableGroups)
    {
        var batchSize = 5;
        var config = new QueueServiceConfig
        {
            WaitingIntervalStartMs = 50,
            WaitingIntervalFactor = 2,
            WaitingIntervalMaxMs = 1000,
            Queues =
            [
                new()
                {
                    QueueName = "qname",
                    MaxBatchSize = 3,
                    DisableSerializableGroups = disableSerializableGroups
                }
            ]
        };
        var queueService = CreateQueueService(config);
        
        await queueService.ReadBatch(batchSize, new List<JobExecutionModel>());

        var expectedRequest = new GetJobsRequest
        {
            QueueName = "qname",
            BatchSize = 3,
            ServerId = ServerId,
            DisableSerializableGroups = disableSerializableGroups
        };
        _storageMock.VerifyTakeToProcessing(expectedRequest);
    }

    private SingleQueueService<JobExecutionModel> CreateQueueService(QueueServiceConfig config)
    {
        var reader = new TakeToProcessingJobsQueueReader(_storageMock.Object);
        return new SingleQueueService<JobExecutionModel>(reader, config, ServerId);
    }

    private QueueServiceConfig GetQueueServiceConfig()
    {
        return new QueueServiceConfig
        {
            WaitingIntervalStartMs = 50,
            WaitingIntervalFactor = 2,
            WaitingIntervalMaxMs = 1000,
            Queues =
            [
                new()
                {
                    QueueName = QueueSettings.DefaultQueueName,
                    MaxBatchSize = 10,
                    DisableSerializableGroups = false,
                }
            ]
        };
    }
}