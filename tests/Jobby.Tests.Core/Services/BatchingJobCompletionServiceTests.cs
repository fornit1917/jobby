using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Moq;

namespace Jobby.Tests.Core.Services;

public class BatchingJobCompletionServiceTests
{
    private readonly Mock<IJobbyStorage> _storageMock;

    private readonly List<Guid> _completedJobIds;
    private readonly List<Guid> _unlockedNextJobIds;

    private bool _bulkDeleteCalled = false;
    private bool _bulkMarkCompletedCalled = false;

    public BatchingJobCompletionServiceTests()
    {
        _completedJobIds = new List<Guid>();
        _unlockedNextJobIds = new List<Guid>();

        _storageMock = new Mock<IJobbyStorage>();

        _storageMock
            .Setup(x => x.BulkDeleteProcessingJobsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<IReadOnlyList<Guid>>()))
            .Callback<IReadOnlyList<Guid>, IReadOnlyList<Guid>>((jobIds, nextJobIds) =>
            {
                _completedJobIds.AddRange(jobIds);
                _unlockedNextJobIds.AddRange(nextJobIds);
                _bulkDeleteCalled = true;
            });

        _storageMock
            .Setup(x => x.BulkUpdateProcessingJobsToCompletedAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<IReadOnlyList<Guid>>()))
            .Callback<IReadOnlyList<Guid>, IReadOnlyList<Guid>>((jobIds, nextJobIds) =>
            {
                _completedJobIds.AddRange(jobIds);
                _unlockedNextJobIds.AddRange(nextJobIds);
                _bulkMarkCompletedCalled = true;
            });
    }

    [Fact]
    public async Task CompleteJob_DeleteCompletedTrue_DeletesJobs()
    {
        var settings = new JobbyServerSettings { DeleteCompleted = true };
        using var service = new BatchingJobCompletionService(_storageMock.Object, settings);
        var jobId = Guid.NewGuid();
        var nextJobId = Guid.NewGuid();

        await service.CompleteJob(jobId, nextJobId);

        Assert.Contains(jobId, _completedJobIds);
        Assert.True(_bulkDeleteCalled);
        Assert.False(_bulkMarkCompletedCalled);
    }

    [Fact]
    public async Task CompleteJob_DeleteCompletedTrue_MarksCompleted()
    {
        var settings = new JobbyServerSettings { DeleteCompleted = false };
        using var service = new BatchingJobCompletionService(_storageMock.Object, settings);
        var jobId = Guid.NewGuid();
        var nextJobId = Guid.NewGuid();

        await service.CompleteJob(jobId, nextJobId);

        Assert.Contains(jobId, _completedJobIds);
        Assert.False(_bulkDeleteCalled);
        Assert.True(_bulkMarkCompletedCalled);
    }
}
