using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq.Expressions;

namespace Jobby.Tests.Core.Services;

public class JobPostProcessingServiceTests
{
    private readonly Mock<IJobbyStorage> _storageMock;
    private readonly Mock<IJobCompletionService> _completionServiceMock;
    private readonly Mock<ILogger<JobPostProcessingService>> _loggerMock;

    private readonly JobPostProcessingService _postProcessingService;

    public JobPostProcessingServiceTests()
    {
        _storageMock = new Mock<IJobbyStorage>();
        _completionServiceMock = new Mock<IJobCompletionService>();
        _loggerMock = new Mock<ILogger<JobPostProcessingService>>();
        _postProcessingService = new JobPostProcessingService(_storageMock.Object, _completionServiceMock.Object, _loggerMock.Object);
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

        _completionServiceMock.Verify(x => x.CompleteJob(job.Id, job.NextJobId), Times.Once);
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

        Expression<Func<DateTime, bool>> verifyNewStartTime = x => x.Subtract(DateTime.UtcNow) > TimeSpan.FromSeconds(9)
                                                                   && x.Subtract(DateTime.UtcNow) < TimeSpan.FromSeconds(11);
        _storageMock.Verify(x => x.RescheduleProcessingJobAsync(job.Id, It.Is(verifyNewStartTime), failedReason), Times.Once);
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

        _storageMock.Verify(x => x.UpdateProcessingJobToFailedAsync(job.Id, failedReason), Times.Once);
        Assert.True(_postProcessingService.IsRetryQueueEmpty);
    }

    [Fact]
    public async Task RescheduleRecurrent_Reschedules()
    {
        var job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            Cron = "0 3 1 12 *"
        };
        var error = "error";

        await _postProcessingService.RescheduleRecurrent(job, error);

        Expression<Func<DateTime, bool>> verifyNewStartTime = x => CronHelper.GetNext(job.Cron, DateTime.UtcNow).Subtract(x) < TimeSpan.FromSeconds(1);
        _storageMock.Verify(x => x.RescheduleProcessingJobAsync(job.Id, It.Is(verifyNewStartTime), error), Times.Once());
        Assert.True(_postProcessingService.IsRetryQueueEmpty);
    }

    [Fact]
    public async Task DoRetriesFromQueue_CompletedJob_RetriesHandleCompleted()
    {
        var job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            NextJobId = Guid.NewGuid(),
        };
        _completionServiceMock
            .SetupSequence(x => x.CompleteJob(job.Id, job.NextJobId))
            .Throws(new Exception())
            .Returns(Task.CompletedTask);

        await _postProcessingService.HandleCompleted(job);

        Assert.False(_postProcessingService.IsRetryQueueEmpty);

        await _postProcessingService.DoRetriesFromQueue();

        Assert.True(_postProcessingService.IsRetryQueueEmpty);
        _completionServiceMock.Verify(x => x.CompleteJob(job.Id, job.NextJobId), Times.Exactly(2));
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
            .SetupSequence(x => x.UpdateProcessingJobToFailedAsync(job.Id, error))
            .Throws(new Exception())
            .Returns(Task.CompletedTask);

        await _postProcessingService.HandleFailed(job, retryPolicy, error);

        Assert.False(_postProcessingService.IsRetryQueueEmpty);

        await _postProcessingService.DoRetriesFromQueue();

        Assert.True(_postProcessingService.IsRetryQueueEmpty);
        _storageMock.Verify(x => x.UpdateProcessingJobToFailedAsync(job.Id, error), Times.Exactly(2));
    }

    [Fact]
    public async Task DoRetriesFromQueue_RecurrentJob_RetriesRescheduleRecurrent()
    {
        var job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            Cron = "0 3 1 12 *"
        };
        var error = "error";
        _storageMock
            .SetupSequence(x => x.RescheduleProcessingJobAsync(job.Id, It.IsAny<DateTime>(), error))
            .Throws(new Exception())
            .Returns(Task.CompletedTask);

        await _postProcessingService.RescheduleRecurrent(job, error);

        Assert.False(_postProcessingService.IsRetryQueueEmpty);

        await _postProcessingService.DoRetriesFromQueue();

        Assert.True(_postProcessingService.IsRetryQueueEmpty);
        _storageMock.Verify(x => x.RescheduleProcessingJobAsync(job.Id, It.IsAny<DateTime>(), error));
    }

    [Fact]
    public void Dispose_CallsDisposeInJobCompletionServiceIfItIsDisposable()
    {
        var completionService = new DisposableCompletionService();
        var postProcessingService = new JobPostProcessingService(_storageMock.Object, completionService, _loggerMock.Object);

        postProcessingService.Dispose();

        Assert.True(completionService.Disposed);
    }

    private class DisposableCompletionService : IJobCompletionService, IDisposable
    {
        public bool Disposed { get; private set; } = false;

        public Task CompleteJob(Guid jobId, Guid? nextJobId)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
