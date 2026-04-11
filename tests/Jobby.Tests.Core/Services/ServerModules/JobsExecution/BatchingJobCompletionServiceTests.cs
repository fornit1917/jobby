using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services.ServerModules.JobsExecution;
using Moq;

namespace Jobby.Tests.Core.Services.ServerModules.JobsExecution;

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
            .Setup(x => x.BulkDeleteProcessingJobsAsync(It.IsAny<CompleteJobsBatch>()))
            .Callback<CompleteJobsBatch>(jobs =>
            {
                _completedJobIds.AddRange(jobs.JobIds);
                _passedServerIds.Add(jobs.ServerId);
                _unlockedNextJobIds.AddRange(jobs.NextJobIds);
                _bulkDeleteCalled = true;
            });

        _storageMock
            .Setup(x => x.BulkUpdateProcessingJobsToCompletedAsync(It.IsAny<CompleteJobsBatch>()))
            .Callback<CompleteJobsBatch>(jobs =>
            {
                _completedJobIds.AddRange(jobs.JobIds);
                _passedServerIds.Add(jobs.ServerId);
                _unlockedNextJobIds.AddRange(jobs.NextJobIds);
                _bulkMarkCompletedCalled = true;
            });
    }

    [Fact]
    public async Task CompleteJob_HasNext_DeleteCompletedTrue_DeletesJobsWithUnlock()
    {
        var settings = new JobbyServerSettings { DeleteCompleted = true };
        var service = new BatchingJobCompletionService(_storageMock.Object, settings, ServerId);
        var job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            NextJobId = Guid.NewGuid(),
            ServerId = ServerId
        };

        await service.CompleteJob(job);

        Assert.Contains(job.Id, _completedJobIds);
        Assert.Contains(ServerId, _passedServerIds);
        Assert.Contains(job.NextJobId.Value, _unlockedNextJobIds);
        Assert.DoesNotContain(_passedServerIds, x => x != ServerId);
        Assert.True(_bulkDeleteCalled);
        Assert.False(_bulkMarkCompletedCalled);
    }

    [Fact]
    public async Task CompleteJob_DoesNotHaveNext_DeleteCompletedFalse_MarksCompleted()
    {
        var settings = new JobbyServerSettings { DeleteCompleted = false };
        var service = new BatchingJobCompletionService(_storageMock.Object, settings, ServerId);
        var job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            NextJobId = null,
            ServerId = ServerId
        };

        await service.CompleteJob(job);

        Assert.Contains(job.Id, _completedJobIds);
        
        Assert.Contains(ServerId, _passedServerIds);
        Assert.DoesNotContain(_passedServerIds, x => x != ServerId);
        Assert.Empty(_unlockedNextJobIds);
        Assert.False(_bulkDeleteCalled);
        Assert.True(_bulkMarkCompletedCalled);
    }
}
