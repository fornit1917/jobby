using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Moq;
using System.Linq.Expressions;

namespace Jobby.Tests.Core.Services;

public class SimpleJobCompletionServiceTests
{
    private readonly Mock<IJobbyStorage> _storageMock = new Mock<IJobbyStorage>();

    private const string ServerId = "serverId";

    private SimpleJobCompletionService? _service;

    [Fact]
    public async Task CompleteJob_DeleteCompletedTrue_DeletesJob()
    {
        _service = new SimpleJobCompletionService(_storageMock.Object, deleteCompletedJobs: true, ServerId);
        var jobId = Guid.NewGuid();
        var nextJobId = Guid.NewGuid();
        string? sequenceId = null;

        await _service.CompleteJob(jobId, nextJobId, sequenceId);

        Expression<Func<ProcessingJob, bool>> expectedJob = x => x.JobId == jobId && x.ServerId == ServerId;
        _storageMock.Verify(x => x.DeleteProcessingJobAsync(It.Is(expectedJob), nextJobId, sequenceId), Times.Once);
        _storageMock.Verify(x => x.UpdateProcessingJobToCompletedAsync(It.Is(expectedJob), nextJobId, sequenceId), Times.Never);
    }

    [Fact]
    public async Task CompleteJob_DeleteCompletedFalse_MarksCompleted()
    {
        _service = new SimpleJobCompletionService(_storageMock.Object, deleteCompletedJobs: false, ServerId);
        var jobId = Guid.NewGuid();
        var nextJobId = Guid.NewGuid();
        string? sequenceId = null;

        await _service.CompleteJob(jobId, nextJobId, sequenceId);

        Expression<Func<ProcessingJob, bool>> expectedJob = x => x.JobId == jobId && x.ServerId == ServerId;
        _storageMock.Verify(x => x.DeleteProcessingJobAsync(It.Is(expectedJob), nextJobId, sequenceId), Times.Never);
        _storageMock.Verify(x => x.UpdateProcessingJobToCompletedAsync(It.Is(expectedJob), nextJobId, sequenceId), Times.Once);
    }
}
