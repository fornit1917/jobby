using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.HandlerPipeline;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jobby.Tests.Core.Services;

public class JobExecutionServiceTests
{
    private readonly Mock<IJobExecutionScopeFactory> _scopeFactoryMock;
    private readonly Mock<IJobsRegistry> _jobsRegistryMock;
    private readonly Mock<IRetryPolicyService> _retryPolicyServiceMock;
    private readonly Mock<IJobParamSerializer> _serializerMock;
    private readonly Mock<IPipelineBuilder> _pipelineBuilderMock;
    private readonly Mock<IJobPostProcessingService> _postProcessingServiceMock;
    private readonly Mock<ILogger<JobExecutionService>> _loggerMock;

    private readonly Mock<IJobExecutor> _jobExecutorMock;
    private readonly Mock<IJobExecutionScope> _scopeMock;

    private const string JobName = "JobName";
    private static readonly RetryPolicy UsingRetryPolicy = RetryPolicy.NoRetry;

    private readonly IJobExecutionService _executionService;

    public JobExecutionServiceTests()
    {
        _scopeFactoryMock = new Mock<IJobExecutionScopeFactory>();
        _scopeMock = new Mock<IJobExecutionScope>();
        _scopeFactoryMock.Setup(x => x.CreateJobExecutionScope()).Returns(_scopeMock.Object);

        _jobsRegistryMock = new Mock<IJobsRegistry>();
        _jobExecutorMock = new Mock<IJobExecutor>();
        _jobsRegistryMock.Setup(x => x.GetJobExecutor(JobName)).Returns(_jobExecutorMock.Object);

        _retryPolicyServiceMock = new Mock<IRetryPolicyService>();

        _serializerMock = new Mock<IJobParamSerializer>();

        _pipelineBuilderMock = new Mock<IPipelineBuilder>();
        
        _postProcessingServiceMock = new Mock<IJobPostProcessingService>();
        
        _loggerMock = new Mock<ILogger<JobExecutionService>>();

        _executionService = new JobExecutionService(_scopeFactoryMock.Object,
            _jobsRegistryMock.Object,
            _retryPolicyServiceMock.Object,
            _serializerMock.Object,
            _pipelineBuilderMock.Object,
            _postProcessingServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteJob_NotRecurrent_Ok_ExecutesAndHandlesCompleted()
    {
        var job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            JobName = JobName,
            StartedCount = 1,
            JobParam = "jobParam"
        };
        var cancelationToken = new CancellationTokenSource().Token;
        var retryPolicy = new RetryPolicy 
        {
            MaxCount = 10,
            IntervalsSeconds = [10]
        };
        SetupRetryPolicyMock(job, retryPolicy);
        
        await _executionService.ExecuteJob(job, cancelationToken);

        var expectedCtx = new JobExecutionContext
        {
            CancellationToken = cancelationToken,
            IsLastAttempt = false,
            JobName = job.JobName,
            IsRecurrent = job.IsRecurrent,
            StartedCount = job.StartedCount,
        };
        _jobExecutorMock
            .Verify(x => x.Execute(job, expectedCtx, _scopeMock.Object, _serializerMock.Object, _pipelineBuilderMock.Object), Times.Once);
        _postProcessingServiceMock.Verify(x => x.HandleCompleted(job), Times.Once);
        _postProcessingServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteJob_NotRecurrent_Error_ExecutesAndHandlesFailed()
    {
        var job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            JobName = JobName,
            StartedCount = 1,
            JobParam = "jobParam"
        };
        var cancelationToken = new CancellationTokenSource().Token;
        var retryPolicy = RetryPolicy.NoRetry;
        SetupRetryPolicyMock(job, retryPolicy);

        var expectedCtx = new JobExecutionContext
        {
            CancellationToken = cancelationToken,
            IsLastAttempt = true,
            JobName = job.JobName,
            IsRecurrent = job.IsRecurrent,
            StartedCount = job.StartedCount,
        };
        var ex = new Exception("error");
        _jobExecutorMock
            .Setup(x => x.Execute(job, expectedCtx, _scopeMock.Object, _serializerMock.Object, _pipelineBuilderMock.Object))
            .ThrowsAsync(ex);

        await _executionService.ExecuteJob(job, cancelationToken);

        _jobExecutorMock
            .Verify(x => x.Execute(job, expectedCtx, _scopeMock.Object, _serializerMock.Object, _pipelineBuilderMock.Object), Times.Once);
        _postProcessingServiceMock.Verify(x => x.HandleFailed(job, retryPolicy, ex.ToString()));
        _postProcessingServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteJob_Recurrent_Ok_ExecutesAndReschedules()
    {
        var job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            JobName = JobName,
            JobParam = "jobParam",
            Cron = "*/5 * * * *"
        };
        var cancelationToken = new CancellationTokenSource().Token;
        var retryPolicy = RetryPolicy.NoRetry;
        SetupRetryPolicyMock(job, retryPolicy);

        await _executionService.ExecuteJob(job, cancelationToken);

        var expectedCtx = new JobExecutionContext
        {
            CancellationToken = cancelationToken,
            IsLastAttempt = false,
            JobName = job.JobName,
            IsRecurrent = job.IsRecurrent,
            StartedCount = job.StartedCount,
        };
        _jobExecutorMock
            .Verify(x => x.Execute(job, expectedCtx, _scopeMock.Object, _serializerMock.Object, _pipelineBuilderMock.Object), Times.Once);
        _postProcessingServiceMock.Verify(x => x.RescheduleRecurrent(job, null));
        _postProcessingServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteJob_Recurrent_Error_ExecutesAndReschedules()
    {
        var job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            JobName = JobName,
            JobParam = "jobParam",
            Cron = "*/5 * * * *"
        };
        var cancelationToken = new CancellationTokenSource().Token;
        var retryPolicy = RetryPolicy.NoRetry;
        SetupRetryPolicyMock(job, retryPolicy);

        var expectedCtx = new JobExecutionContext
        {
            CancellationToken = cancelationToken,
            IsLastAttempt = false,
            JobName = job.JobName,
            IsRecurrent = job.IsRecurrent,
            StartedCount = job.StartedCount,
        };
        var ex = new Exception("error");
        _jobExecutorMock
            .Setup(x => x.Execute(job, expectedCtx, _scopeMock.Object, _serializerMock.Object, _pipelineBuilderMock.Object))
            .ThrowsAsync(ex);

        await _executionService.ExecuteJob(job, cancelationToken);

        _jobExecutorMock
            .Verify(x => x.Execute(job, expectedCtx, _scopeMock.Object, _serializerMock.Object, _pipelineBuilderMock.Object), Times.Once);
        _postProcessingServiceMock.Verify(x => x.RescheduleRecurrent(job, ex.ToString()));
        _postProcessingServiceMock.VerifyNoOtherCalls();
    }

    private void SetupRetryPolicyMock(JobExecutionModel job, RetryPolicy retryPolicy)
    {
        _retryPolicyServiceMock.Setup(x => x.GetRetryPolicy(job)).Returns(retryPolicy);
    }
}
