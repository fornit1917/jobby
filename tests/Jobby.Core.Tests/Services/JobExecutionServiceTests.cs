using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.TestsUtils.Jobs;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jobby.Core.Tests.Services;

public class JobExecutionServiceTests
{
    private readonly Mock<IJobPostProcessingService> _postProcessingServiceMock;

    private readonly IJobExecutionService _executionService;

    private TestJobCommand? _jobCommand;
    private JobExecutionModel? _job;
    private readonly TestJobCommandHandler _handler;
    private const string SerializedCommand = "serialized";
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

        var execMetadata = new JobExecutionMetadata
        {
            CommandType = typeof(TestJobCommand),
            HandlerType = typeof(IJobCommandHandler<TestJobCommand>),
            HandlerImplType = typeof(TestJobCommandHandler),
            ExecMethod = typeof(TestJobCommandHandler).GetMethod(nameof(TestJobCommandHandler.ExecuteAsync))
                    ?? throw new Exception("Method ExecuteAsync not found")
        };
        jobsRegistryMock
            .Setup(x => x.GetJobExecutionMetadata(TestJobCommand.GetJobName()))
            .Returns(execMetadata);

        retryPolicyServiceMock.Setup(x => x.GetRetryPolicy(It.Is<JobExecutionModel>(x => x == _job))).Returns(UsingRetryPolicy);

        _handler = new TestJobCommandHandler();

        var scopeMock = new Mock<IJobExecutionScope>();
        scopeMock.Setup(x => x.GetService(execMetadata.HandlerType)).Returns(_handler);
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
