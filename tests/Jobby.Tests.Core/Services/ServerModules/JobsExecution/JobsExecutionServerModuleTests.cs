using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Queues;
using Jobby.Core.Interfaces.ServerModules.JobsExecution;
using Jobby.Core.Models;
using Jobby.Core.Services.ServerModules.JobsExecution;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jobby.Tests.Core.Services.ServerModules.JobsExecution;

public class JobsExecutionServerModuleTests
{
    private readonly Mock<IJobbyStorage> _storageMock;
    private readonly Mock<IQueueService<JobExecutionModel>> _queueServiceMock;
    private readonly Mock<IQueueServiceFactory> _queueServiceFactoryMock;
    private readonly Mock<IJobExecutionService> _executionServiceMock;
    private readonly Mock<IJobPostProcessingService> _postProcessingServiceMock;
    private readonly Mock<ITimerService>  _timerServiceMock;
    private readonly Mock<ILogger<JobsExecutionServerModule>> _loggerMock;
    
    private const string ServerId = "ServerId";

    public JobsExecutionServerModuleTests()
    {
        _storageMock = new Mock<IJobbyStorage>();
        
        _queueServiceMock = new Mock<IQueueService<JobExecutionModel>>();
        _queueServiceMock
            .Setup(x => x.GetWaitingIntervalMs())
            .Returns(0);
        _queueServiceFactoryMock = new Mock<IQueueServiceFactory>();
        _queueServiceFactoryMock
            .Setup(x => x.Create(It.IsAny<IQueueItemsReader<JobExecutionModel>>(), It.IsAny<QueueServiceConfig>(), ServerId))
            .Returns(_queueServiceMock.Object);

        _timerServiceMock = new Mock<ITimerService>();
        
        _executionServiceMock = new Mock<IJobExecutionService>();
        _postProcessingServiceMock = new Mock<IJobPostProcessingService>();
        _loggerMock = new Mock<ILogger<JobsExecutionServerModule>>();
    }

    [Fact]
    public void Constructor_CreatesQueueServiceWithCorrectConfig()
    {
        var settings = new JobbyServerSettings
        {
            PollingIntervalMs = 1000,
            PollingIntervalStartMs = 100,
            PollingIntervalFactor = 2,
            MaxDegreeOfParallelism = 10,
            Queues =
            [
                new()
                {
                    QueueName = "q1",
                },
                new()
                {
                    QueueName = "q2",
                    MaxDegreeOfParallelism = 5,
                    DisableSerializableGroups = true
                }
            ]
        };

        QueueServiceConfig? usedQueueServiceConfig = null;
        _queueServiceFactoryMock
            .Setup(x => x.Create(It.IsAny<IQueueItemsReader<JobExecutionModel>>(), It.IsAny<QueueServiceConfig>(), ServerId))
            .Callback<IQueueItemsReader<JobExecutionModel>, QueueServiceConfig, string>((_, c, _)  => usedQueueServiceConfig = c)
            .Returns(_queueServiceMock.Object);
        CreateJobsExecutionServerModule(settings);
        
        Assert.NotNull(usedQueueServiceConfig);
        Assert.Equal(settings.PollingIntervalMs, usedQueueServiceConfig.WaitingIntervalMaxMs);
        Assert.Equal(settings.PollingIntervalStartMs, usedQueueServiceConfig.WaitingIntervalStartMs);
        Assert.Equal(settings.PollingIntervalFactor, usedQueueServiceConfig.WaitingIntervalFactor);
        Assert.Equal(2, usedQueueServiceConfig.Queues.Count);
        Assert.Equal("q1",  usedQueueServiceConfig.Queues[0].QueueName);
        Assert.Equal(10, usedQueueServiceConfig.Queues[0].MaxBatchSize);
        Assert.False(usedQueueServiceConfig.Queues[0].DisableSerializableGroups);
        Assert.Equal("q2",  usedQueueServiceConfig.Queues[1].QueueName);
        Assert.Equal(5, usedQueueServiceConfig.Queues[1].MaxBatchSize);
        Assert.True(usedQueueServiceConfig.Queues[1].DisableSerializableGroups);
    }
    
    [Fact]
    public async Task HappyPath_ExecutesJob()
    {
        var settings = new JobbyServerSettings
        {
            MaxDegreeOfParallelism = 1,
            TakeToProcessingBatchSize = 1,
        };
        var jobsExecutionServerModule = CreateJobsExecutionServerModule(settings);
        var job = new JobExecutionModel();
        var firstCall = true;
        _queueServiceMock
            .Setup(x => x.ReadBatch(settings.TakeToProcessingBatchSize, It.IsAny<List<JobExecutionModel>>()))
            .Callback<int, List<JobExecutionModel>>((_, jobs) =>
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
        
        jobsExecutionServerModule.Start();

        for (var i = 0; i < 100; i++)
        {
            if (executed)
                break;
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        jobsExecutionServerModule.SendStopSignal();
        
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
        var jobsExecutionServerModule = CreateJobsExecutionServerModule(settings);
        var postprocessingRetried = false;
        _postProcessingServiceMock.SetupGet(x => x.IsRetryQueueEmpty).Returns(() => false);
        _postProcessingServiceMock
            .Setup(x => x.DoRetriesFromQueue(It.IsAny<CancellationToken>()))
            .Callback(() => postprocessingRetried = true);

        jobsExecutionServerModule.Start();

        for (var i = 0; i < 100; i++)
        {
            if (postprocessingRetried)
                break;
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        jobsExecutionServerModule.SendStopSignal();
        
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
        var jobsExecutionServerModule = CreateJobsExecutionServerModule(settings);
        var firstCall = true;
        var calledTwoTimes = false;
        _queueServiceMock
            .Setup(x => x.ReadBatch(settings.TakeToProcessingBatchSize, It.IsAny<List<JobExecutionModel>>()))
            .Callback<int, List<JobExecutionModel>>((_, jobs) =>
            {
                jobs.Clear();
                if (firstCall)
                {
                    firstCall = false;
                    throw new Exception("test error");
                }

                calledTwoTimes = true;
            });

        _postProcessingServiceMock.SetupGet(x => x.IsRetryQueueEmpty).Returns(true);

        jobsExecutionServerModule.Start();

        for (var i = 0; i < 100; i++)
        {
            if (calledTwoTimes)
                break;
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        jobsExecutionServerModule.SendStopSignal();
        
        Assert.True(calledTwoTimes);
    }

    private JobsExecutionServerModule CreateJobsExecutionServerModule(JobbyServerSettings settings)
    {
        return new JobsExecutionServerModule(_storageMock.Object,
            _queueServiceFactoryMock.Object,
            _executionServiceMock.Object,
            _postProcessingServiceMock.Object,
            _timerServiceMock.Object,
            _loggerMock.Object,
            settings,
            ServerId
        );
    }
}