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

    private const string ServerId = "serverId";

    public JobPostProcessingServiceTests()
    {
        _storageMock = new Mock<IJobbyStorage>();
        _completionServiceMock = new Mock<IJobCompletionService>();
        _loggerMock = new Mock<ILogger<JobPostProcessingService>>();
        _postProcessingService = new JobPostProcessingService(
            _storageMock.Object,
            _completionServiceMock.Object,
            _loggerMock.Object,
            ServerId);
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

        _completionServiceMock.Verify(x => x.CompleteJob(job.Id, job.NextJobId, job.SequenceId), Times.Once);
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
        Expression<Func<ProcessingJob, bool>> expectedJob = x => x.JobId == job.Id && x.ServerId == ServerId;
        _storageMock.Verify(x => x.RescheduleProcessingJobAsync(It.Is(expectedJob), It.Is(expectedNewStartTime), failedReason), Times.Once);
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

        Expression<Func<ProcessingJob, bool>> expectedJob = x => x.JobId == job.Id && x.ServerId == ServerId;
        _storageMock.Verify(x => x.UpdateProcessingJobToFailedAsync(It.Is(expectedJob), failedReason), Times.Once);
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

        Expression<Func<DateTime, bool>> expectedNewStartTime = x => CronHelper.GetNext(job.Cron, DateTime.UtcNow).Subtract(x) < TimeSpan.FromSeconds(1);
        Expression<Func<ProcessingJob, bool>> expectedJob = x => x.JobId == job.Id && x.ServerId == ServerId;
        _storageMock.Verify(x => x.RescheduleProcessingJobAsync(It.Is(expectedJob), It.Is(expectedNewStartTime), error), Times.Once());
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
            .SetupSequence(x => x.CompleteJob(job.Id, job.NextJobId, job.SequenceId))
            .Throws(new Exception())
            .Returns(Task.CompletedTask);

        await _postProcessingService.HandleCompleted(job);

        Assert.False(_postProcessingService.IsRetryQueueEmpty);

        await _postProcessingService.DoRetriesFromQueue();

        Assert.True(_postProcessingService.IsRetryQueueEmpty);
        _completionServiceMock.Verify(x => x.CompleteJob(job.Id, job.NextJobId, job.SequenceId), Times.Exactly(2));
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
        Expression<Func<ProcessingJob, bool>> expectedJob = x => x.JobId == job.Id && x.ServerId == ServerId;
        _storageMock
            .SetupSequence(x => x.UpdateProcessingJobToFailedAsync(It.Is(expectedJob), error))
            .Throws(new Exception())
            .Returns(Task.CompletedTask);

        await _postProcessingService.HandleFailed(job, retryPolicy, error);

        Assert.False(_postProcessingService.IsRetryQueueEmpty);

        await _postProcessingService.DoRetriesFromQueue();

        Assert.True(_postProcessingService.IsRetryQueueEmpty);
        _storageMock.Verify(x => x.UpdateProcessingJobToFailedAsync(It.Is(expectedJob), error), Times.Exactly(2));
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
        Expression<Func<ProcessingJob, bool>> expectedJob = x => x.JobId == job.Id && x.ServerId == ServerId;
        _storageMock
            .SetupSequence(x => x.RescheduleProcessingJobAsync(It.Is(expectedJob), It.IsAny<DateTime>(), error))
            .Throws(new Exception())
            .Returns(Task.CompletedTask);

        await _postProcessingService.RescheduleRecurrent(job, error);

        Assert.False(_postProcessingService.IsRetryQueueEmpty);

        await _postProcessingService.DoRetriesFromQueue();

        Assert.True(_postProcessingService.IsRetryQueueEmpty);
        _storageMock.Verify(x => x.RescheduleProcessingJobAsync(It.Is(expectedJob), It.IsAny<DateTime>(), error));
    }

    [Fact]
    public void Dispose_CallsDisposeInJobCompletionServiceIfItIsDisposable()
    {
        var completionService = new DisposableCompletionService();
        var postProcessingService = new JobPostProcessingService(_storageMock.Object, completionService, _loggerMock.Object, ServerId);

        postProcessingService.Dispose();

        Assert.True(completionService.Disposed);
    }

    private class DisposableCompletionService : IJobCompletionService, IDisposable
    {
        public bool Disposed { get; private set; } = false;

        public Task CompleteJob(Guid jobId, Guid? nextJobId, string? sequenceId)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
