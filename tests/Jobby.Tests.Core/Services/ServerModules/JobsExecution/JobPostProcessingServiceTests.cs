using System.Linq.Expressions;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Interfaces.ServerModules.JobsExecution;
using Jobby.Core.Models;
using Jobby.Core.Services.ServerModules.JobsExecution;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jobby.Tests.Core.Services.ServerModules.JobsExecution;

public class JobPostProcessingServiceTests
{
    private readonly Mock<IJobbyStorage> _storageMock;
    private readonly Mock<IJobCompletionService> _completionServiceMock;
    private readonly Mock<ILogger<JobPostProcessingService>> _loggerMock;
    private readonly Mock<IJobParamSerializer> _jobParamSerializerMock;
    private readonly Mock<IScheduleExecutor> _schedulerMock;

    private readonly JobPostProcessingService _postProcessingService;

    private const string SchedulerType = "scheduler-type";

    public JobPostProcessingServiceTests()
    {
        _storageMock = new Mock<IJobbyStorage>();
        _completionServiceMock = new Mock<IJobCompletionService>();
        _loggerMock = new Mock<ILogger<JobPostProcessingService>>();
        _schedulerMock = new Mock<IScheduleExecutor>();
        _jobParamSerializerMock = new Mock<IJobParamSerializer>();
        var schedulers = new Dictionary<string, IScheduleExecutor>
        {
            [SchedulerType] = _schedulerMock.Object,
        };
        _postProcessingService = new JobPostProcessingService(
            _storageMock.Object,
            _completionServiceMock.Object,
            schedulers,
            _jobParamSerializerMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task HandleCompleted_CompletesJob()
    {
        var job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            NextJobId = Guid.NewGuid(),
        };

        await _postProcessingService.HandleCompleted(job);

        _completionServiceMock.Verify(x => x.CompleteJob(job), Times.Once);
        Assert.True(_postProcessingService.IsRetryQueueEmpty);
    }

    [Fact]
    public async Task HandleFailed_Retriable_Reschedules()
    {
        var job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            StartedCount = 1,
        };
        var retryPolicy = new RetryPolicy
        {
            IntervalsSeconds = [10],
            MaxCount = 100
        };
        var failedReason = "errorMessage";

        await _postProcessingService.HandleFailed(job, retryPolicy, failedReason);

        Expression<Func<DateTime, bool>> expectedNewStartTime = x => x.Subtract(DateTime.UtcNow) > TimeSpan.FromSeconds(9)
                                                                   && x.Subtract(DateTime.UtcNow) < TimeSpan.FromSeconds(11);
        _storageMock.Verify(x => x.RescheduleProcessingJobAsync(job, It.Is(expectedNewStartTime), failedReason), Times.Once);
        Assert.True(_postProcessingService.IsRetryQueueEmpty);
    }

    [Fact]
    public async Task HandleFailed_NotRetriable_MarksFailed()
    {
        var job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            StartedCount = 2,
        };
        var retryPolicy = new RetryPolicy
        {
            IntervalsSeconds = [10],
            MaxCount = 2
        };
        var failedReason = "errorMessage";

        await _postProcessingService.HandleFailed(job, retryPolicy, failedReason);
        
        _storageMock.Verify(x => x.UpdateProcessingJobToFailedAsync(job, failedReason), Times.Once);
        Assert.True(_postProcessingService.IsRetryQueueEmpty);
    }
    /*
    [Fact]
    public async Task RescheduleRecurrent_Reschedules()
    {
        var job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            Schedule = "0 3 1 12 *",
            SchedulerType = SchedulerType,
            ScheduledStartAt = DateTime.UtcNow,
        };
        var error = "error";
        var expectedNextTime = DateTime.UtcNow.AddSeconds(123);
        _schedulerMock
            .Setup(x => x.GetNextStartTime(job.Schedule, job.ScheduledStartAt))
            .Returns(expectedNextTime);

        await _postProcessingService.RescheduleRecurrent(job, error);

        _storageMock.Verify(x => x.RescheduleProcessingJobAsync(job, expectedNextTime, error), Times.Once());
        Assert.True(_postProcessingService.IsRetryQueueEmpty);
    }
    */
    [Fact]
    public async Task DoRetriesFromQueue_CompletedJob_RetriesHandleCompleted()
    {
        var job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            NextJobId = Guid.NewGuid(),
        };
        _completionServiceMock
            .SetupSequence(x => x.CompleteJob(job))
            .Throws(new Exception())
            .Returns(Task.CompletedTask);

        await _postProcessingService.HandleCompleted(job);

        Assert.False(_postProcessingService.IsRetryQueueEmpty);

        await _postProcessingService.DoRetriesFromQueue(CancellationToken.None);

        Assert.True(_postProcessingService.IsRetryQueueEmpty);
        _completionServiceMock.Verify(x => x.CompleteJob(job), Times.Exactly(2));
    }

    [Fact]
    public async Task DoRetriesFromQueue_FailedJob_RetriesHandleFailed()
    {
        var job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            NextJobId = Guid.NewGuid(),
            StartedCount = 2,
        };
        var retryPolicy = new RetryPolicy
        {
            IntervalsSeconds = [10],
            MaxCount = 2
        };
        var error = "error";
        _storageMock
            .SetupSequence(x => x.UpdateProcessingJobToFailedAsync(job, error))
            .Throws(new Exception())
            .Returns(Task.CompletedTask);

        await _postProcessingService.HandleFailed(job, retryPolicy, error);

        Assert.False(_postProcessingService.IsRetryQueueEmpty);

        await _postProcessingService.DoRetriesFromQueue(CancellationToken.None);

        Assert.True(_postProcessingService.IsRetryQueueEmpty);
        _storageMock.Verify(x => x.UpdateProcessingJobToFailedAsync(job, error), Times.Exactly(2));
    }

    [Fact]
    public async Task DoRetriesFromQueue_RecurrentJob_RetriesRescheduleRecurrent()
    {
        var job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            Schedule = "0 3 1 12 *",
            SchedulerType = SchedulerType
        };
        var error = "error";
        _storageMock
            .SetupSequence(x => x.RescheduleProcessingJobAsync(job, It.IsAny<DateTime>(), error))
            .Throws(new Exception())
            .Returns(Task.CompletedTask);

        await _postProcessingService.RescheduleRecurrent(job, error);

        Assert.False(_postProcessingService.IsRetryQueueEmpty);

        await _postProcessingService.DoRetriesFromQueue(CancellationToken.None);

        Assert.True(_postProcessingService.IsRetryQueueEmpty);
        _storageMock.Verify(x => x.RescheduleProcessingJobAsync(job, It.IsAny<DateTime>(), error));
    }
}