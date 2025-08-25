using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Moq;

namespace Jobby.Tests.Core.Services;

public class BatchingJobCompletionServiceTests
{
    private readonly Mock<IJobbyStorage> _storageMock;

    private const string ServerId = "serverId";

    private readonly List<Guid> _completedJobIds;
    private readonly List<string> _passedServerIds;
    private readonly List<Guid> _unlockedNextJobIds;

    private bool _bulkDeleteCalled = false;
    private bool _bulkMarkCompletedCalled = false;

    public BatchingJobCompletionServiceTests()
    {
        _completedJobIds = new List<Guid>();
        _passedServerIds = new List<string>();
        _unlockedNextJobIds = new List<Guid>();

        _storageMock = new Mock<IJobbyStorage>();

        _storageMock
            .Setup(x => x.BulkDeleteProcessingJobsAsync(It.IsAny<ProcessingJobsList>(), It.IsAny<IReadOnlyList<Guid>>()))
            .Callback<ProcessingJobsList, IReadOnlyList<Guid>>((jobs, nextJobIds) =>
            {
                _completedJobIds.AddRange(jobs.JobIds);
                _passedServerIds.Add(jobs.ServerId);
                _unlockedNextJobIds.AddRange(nextJobIds);
                _bulkDeleteCalled = true;
            });

        _storageMock
            .Setup(x => x.BulkUpdateProcessingJobsToCompletedAsync(It.IsAny<ProcessingJobsList>(), It.IsAny<IReadOnlyList<Guid>>()))
            .Callback<ProcessingJobsList, IReadOnlyList<Guid>>((jobs, nextJobIds) =>
            {
                _completedJobIds.AddRange(jobs.JobIds);
                _passedServerIds.Add(jobs.ServerId);
                _unlockedNextJobIds.AddRange(nextJobIds);
                _bulkMarkCompletedCalled = true;
            });
    }

    [Fact]
    public async Task CompleteJob_DeleteCompletedTrue_DeletesJobs()
    {
        var settings = new JobbyServerSettings { DeleteCompleted = true };
        using var service = new BatchingJobCompletionService(_storageMock.Object, settings, ServerId);
        var jobId = Guid.NewGuid();
        var nextJobId = Guid.NewGuid();

        await service.CompleteJob(jobId, nextJobId);

        Assert.Contains(jobId, _completedJobIds);
        Assert.Contains(ServerId, _passedServerIds);
        Assert.DoesNotContain(_passedServerIds, x => x != ServerId);
        Assert.True(_bulkDeleteCalled);
        Assert.False(_bulkMarkCompletedCalled);
    }

    [Fact]
    public async Task CompleteJob_DeleteCompletedTrue_MarksCompleted()
    {
        var settings = new JobbyServerSettings { DeleteCompleted = false };
        using var service = new BatchingJobCompletionService(_storageMock.Object, settings, ServerId);
        var jobId = Guid.NewGuid();
        var nextJobId = Guid.NewGuid();

        await service.CompleteJob(jobId, nextJobId);

        Assert.Contains(jobId, _completedJobIds);
        Assert.Contains(ServerId, _passedServerIds);
        Assert.DoesNotContain(_passedServerIds, x => x != ServerId);
        Assert.False(_bulkDeleteCalled);
        Assert.True(_bulkMarkCompletedCalled);
    }
}
