using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.TestsUtils.Jobs;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jobby.Tests.Core.Services;

public class JobExecutionServiceTests
{
    private readonly Mock<IJobPostProcessingService> _postProcessingServiceMock;

    private readonly IJobExecutionService _executionService;

    private TestJobCommand? _jobCommand;
    private TestNoAsyncJobCommand? _jobCommandNotAsync;
    private JobExecutionModel? _job;
    private readonly TestJobCommandHandler _handler;
    private readonly TestNoAsyncJobCommandHandler _handlerNotAsync;
    private const string SerializedCommand = "serialized";
    private const string SerializedCommandNotAsync = "serializedNotAsync";
    private static readonly RetryPolicy UsingRetryPolicy = RetryPolicy.NoRetry;

    public JobExecutionServiceTests()
    {
        var scopeFactoryMock = new Mock<IJobExecutionScopeFactory>();
        var jobsRegistryMock = new Mock<IJobsRegistry>();
        var retryPolicyServiceMock = new Mock<IRetryPolicyService>();
        var serializerMock = new Mock<IJobParamSerializer>();
        var loggerMock = new Mock<ILogger<JobExecutionService>>();
        _postProcessingServiceMock = new Mock<IJobPostProcessingService>();

        serializerMock
            .Setup(x => x.DeserializeJobParam(SerializedCommand, typeof(TestJobCommand)))
            .Returns(() => _jobCommand);
        serializerMock
            .Setup(x => x.DeserializeJobParam(SerializedCommandNotAsync, typeof(TestNoAsyncJobCommand)))
            .Returns(() => _jobCommandNotAsync);

        var jobExecutorFactory = new JobExecutorFactory<TestJobCommand, TestJobCommandHandler>();
        
        jobsRegistryMock
            .Setup(x => x.GetJobExecutorFactory(TestJobCommand.GetJobName()))
            .Returns(jobExecutorFactory);

        var jobExecutorFactoryNotAsync = new JobExecutorFactory<TestNoAsyncJobCommand, TestNoAsyncJobCommandHandler>();
        jobsRegistryMock
            .Setup(x => x.GetJobExecutorFactory(TestNoAsyncJobCommand.GetJobName()))
            .Returns(jobExecutorFactoryNotAsync);

        retryPolicyServiceMock.Setup(x => x.GetRetryPolicy(It.Is<JobExecutionModel>(x => x == _job))).Returns(UsingRetryPolicy);

        _handler = new TestJobCommandHandler();
        _handlerNotAsync = new TestNoAsyncJobCommandHandler();

        var scopeMock = new Mock<IJobExecutionScope>();
        scopeMock.Setup(x => x.GetService(jobExecutorFactory.GetJobTypesMetadata().HandlerType)).Returns(_handler);
        scopeMock.Setup(x => x.GetService(jobExecutorFactoryNotAsync.GetJobTypesMetadata().HandlerType)).Returns(_handlerNotAsync);

        scopeFactoryMock
            .Setup(x => x.CreateJobExecutionScope())
            .Returns(scopeMock.Object);

        _executionService = new JobExecutionService(scopeFactoryMock.Object,
            jobsRegistryMock.Object,
            retryPolicyServiceMock.Object,
            serializerMock.Object,
            _postProcessingServiceMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteJob_NotRecurrent_Ok_ExecutesAndHandlesCompleted()
    {
        _job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            JobName = TestJobCommand.GetJobName(),
            StartedCount = 1,
            JobParam = SerializedCommand
        };
        _jobCommand = new TestJobCommand();

        await _executionService.ExecuteJob(_job, CancellationToken.None);

        Assert.Equal(_jobCommand, _handler.LatestCommand);
        _postProcessingServiceMock.Verify(x => x.HandleCompleted(_job), Times.Once);
        _postProcessingServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteJob_NotRecurrent_Error_ExecutesAndHandlesFailed()
    {
        _job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            JobName = TestJobCommand.GetJobName(),
            StartedCount = 1,
            JobParam = SerializedCommand
        };
        _jobCommand = new TestJobCommand
        {
            ExceptionToThrow = new Exception("test error")
        };

        await _executionService.ExecuteJob(_job, CancellationToken.None);

        Assert.Equal(_jobCommand, _handler.LatestCommand);
        _postProcessingServiceMock.Verify(x => x.HandleFailed(_job, UsingRetryPolicy, _jobCommand.ExceptionToThrow.ToString()));
        _postProcessingServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteJob_NotRecurrent_NotAsync_Error_ExecutesAndHandlesFailed()
    {
        _job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            JobName = TestNoAsyncJobCommand.GetJobName(),
            StartedCount = 1,
            JobParam = SerializedCommandNotAsync
        };
        _jobCommandNotAsync = new TestNoAsyncJobCommand
        {
            ExceptionToThrow = new Exception("test error")
        };

        await _executionService.ExecuteJob(_job, CancellationToken.None);

        Assert.Equal(_jobCommandNotAsync, _handlerNotAsync.LatestCommand);
        _postProcessingServiceMock.Verify(x => x.HandleFailed(_job, UsingRetryPolicy, _jobCommandNotAsync.ExceptionToThrow.ToString()));
        _postProcessingServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteJob_Recurrent_Ok_ExecutesAndReschedules()
    {
        _job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            JobName = TestJobCommand.GetJobName(),
            JobParam = SerializedCommand,
            Cron = "*/5 * * * *"
        };
        _jobCommand = new TestJobCommand();

        await _executionService.ExecuteJob(_job, CancellationToken.None);

        Assert.Equal(_jobCommand, _handler.LatestCommand);
        _postProcessingServiceMock.Verify(x => x.RescheduleRecurrent(_job, null));
        _postProcessingServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteJob_Recurrent_Error_ExecutesAndReschedules()
    {
        _job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            JobName = TestJobCommand.GetJobName(),
            JobParam = SerializedCommand,
            Cron = "*/5 * * * *"
        };
        _jobCommand = new TestJobCommand
        {
            ExceptionToThrow = new Exception("test error")
        };

        await _executionService.ExecuteJob(_job, CancellationToken.None);

        Assert.Equal(_jobCommand, _handler.LatestCommand);
        _postProcessingServiceMock.Verify(x => x.RescheduleRecurrent(_job, _jobCommand.ExceptionToThrow.ToString()));
        _postProcessingServiceMock.VerifyNoOtherCalls();
    }
}
