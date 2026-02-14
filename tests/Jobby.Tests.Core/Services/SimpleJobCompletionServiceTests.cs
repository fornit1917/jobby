using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Moq;
using System.Linq.Expressions;

namespace Jobby.Tests.Core.Services;

public class SimpleJobCompletionServiceTests
{
    private readonly Mock<IJobbyStorage> _storageMock = new Mock<IJobbyStorage>();

    private SimpleJobCompletionService? _service;

    [Fact]
    public async Task CompleteJob_DeleteCompletedTrue_DeletesJob()
    {
        _service = new SimpleJobCompletionService(_storageMock.Object, deleteCompletedJobs: true);
        var job = new JobExecutionModel();

        await _service.CompleteJob(job);

        _storageMock.Verify(x => x.DeleteProcessingJobAsync(job), Times.Once);
        _storageMock.Verify(x => x.UpdateProcessingJobToCompletedAsync(job), Times.Never);
    }

    [Fact]
    public async Task CompleteJob_DeleteCompletedFalse_MarksCompleted()
    {
        _service = new SimpleJobCompletionService(_storageMock.Object, deleteCompletedJobs: false);
        var job = new JobExecutionModel();

        await _service.CompleteJob(job);

        _storageMock.Verify(x => x.DeleteProcessingJobAsync(job), Times.Never);
        _storageMock.Verify(x => x.UpdateProcessingJobToCompletedAsync(job), Times.Once);
    }
}
