using Jobby.Core.Interfaces;
using Jobby.Core.Services;
using Moq;

namespace Jobby.Tests.Core.Services;

public class SimpleJobCompletionServiceTests
{
    private readonly Mock<IJobbyStorage> _storageMock = new Mock<IJobbyStorage>();

    private SimpleJobCompletionService? _service;

    [Fact]
    public async Task CompleteJob_DeleteCompletedTrue_DeletesJob()
    {
        _service = new SimpleJobCompletionService(_storageMock.Object, deleteCompletedJobs: true);
        var jobId = Guid.NewGuid();
        var nextJobId = Guid.NewGuid();

        await _service.CompleteJob(jobId, nextJobId);

        _storageMock.Verify(x => x.DeleteAsync(jobId, nextJobId), Times.Once);
        _storageMock.Verify(x => x.MarkCompletedAsync(jobId, nextJobId), Times.Never);
    }

    [Fact]
    public async Task CompleteJob_DeleteCompletedFalse_MarksCompleted()
    {
        _service = new SimpleJobCompletionService(_storageMock.Object, deleteCompletedJobs: false);
        var jobId = Guid.NewGuid();
        var nextJobId = Guid.NewGuid();

        await _service.CompleteJob(jobId, nextJobId);

        _storageMock.Verify(x => x.DeleteAsync(jobId, nextJobId), Times.Never);
        _storageMock.Verify(x => x.MarkCompletedAsync(jobId, nextJobId), Times.Once);
    }
}
