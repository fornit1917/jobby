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
            })
            .Returns(Task.CompletedTask);

        _storageMock
            .Setup(x => x.BulkUpdateProcessingJobsToCompletedAsync(It.IsAny<CompleteJobsBatch>()))
            .Callback<CompleteJobsBatch>(jobs =>
            {
                _completedJobIds.AddRange(jobs.JobIds);
                _passedServerIds.Add(jobs.ServerId);
                _unlockedNextJobIds.AddRange(jobs.NextJobIds);
                _bulkMarkCompletedCalled = true;
            })
            .Returns(Task.CompletedTask);
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

    [Fact]
    public async Task CompleteJob_SeveralJobsWithNextJobIds_PassesAllIdsAndUnlocksAllNextJobs()
    {
        var settings = new JobbyServerSettings
        {
            DeleteCompleted = false,
            MaxDegreeOfParallelism = 10
        };
        // CompleteJobsBatch holds references to the internal lists which are
        // cleared by the service after the storage call, so copy ids in the callback
        var batchJobIds = new List<List<Guid>>();
        var batchNextJobIds = new List<List<Guid>>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storageCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _storageMock.Reset();
        _storageMock
            .Setup(x => x.BulkUpdateProcessingJobsToCompletedAsync(It.IsAny<CompleteJobsBatch>()))
            .Returns(async (CompleteJobsBatch batch) =>
            {
                batchJobIds.Add(batch.JobIds.ToList());
                batchNextJobIds.Add(batch.NextJobIds.ToList());
                storageCalled.TrySetResult();
                await gate.Task;
            });

        var service = new BatchingJobCompletionService(_storageMock.Object, settings, ServerId);
        try
        {
            var job1 = new JobExecutionModel { Id = Guid.NewGuid(), NextJobId = Guid.NewGuid() };
            var job2 = new JobExecutionModel { Id = Guid.NewGuid(), NextJobId = Guid.NewGuid() };
            var job3 = new JobExecutionModel { Id = Guid.NewGuid(), NextJobId = null };

            var task1 = service.CompleteJob(job1);
            await storageCalled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            // While the reader is blocked in storage, the rest of the jobs
            // are buffered and then flushed as a single batch
            var task2 = service.CompleteJob(job2);
            var task3 = service.CompleteJob(job3);

            gate.TrySetResult();
            await Task.WhenAll(task1, task2, task3);

            var allIds = batchJobIds.SelectMany(b => b).ToList();
            Assert.Equal(new[] { job1.Id, job2.Id, job3.Id }.OrderBy(x => x), allIds.OrderBy(x => x));

            var allNextIds = batchNextJobIds.SelectMany(b => b).ToList();
            Assert.Equal(new[] { job1.NextJobId.Value, job2.NextJobId.Value }.OrderBy(x => x), allNextIds.OrderBy(x => x));

            // job3 has no NextJobId and must not unlock anything extra
            Assert.Equal(2, allNextIds.Count);
            _storageMock.Verify(x => x.BulkUpdateProcessingJobsToCompletedAsync(It.IsAny<CompleteJobsBatch>()), Times.AtLeastOnce());
            _storageMock.Verify(x => x.BulkDeleteProcessingJobsAsync(It.IsAny<CompleteJobsBatch>()), Times.Never);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task CompleteJob_StorageFails_AllWaitingTasksGetException()
    {
        var settings = new JobbyServerSettings { MaxDegreeOfParallelism = 10, DeleteCompleted = false };
        var storageError = new Exception("db is down");
        _storageMock.Reset();
        _storageMock
            .Setup(x => x.BulkUpdateProcessingJobsToCompletedAsync(It.IsAny<CompleteJobsBatch>()))
            .ThrowsAsync(storageError);

        var service = new BatchingJobCompletionService(_storageMock.Object, settings, ServerId);
        try
        {
            var jobs = Enumerable.Range(0, 5)
                .Select(_ => new JobExecutionModel { Id = Guid.NewGuid(), NextJobId = null })
                .ToList();

            var tasks = jobs.Select(j => service.CompleteJob(j)).ToList();

            foreach (var task in tasks)
            {
                // WaitAsync turns a regression (error never propagated to the waiting
                // task) into a test failure instead of an infinite hang
                var ex = await Assert.ThrowsAsync<Exception>(() => task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
                Assert.Equal(storageError, ex);
            }
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task CompleteJob_ChannelIsFull_CompletesOnlyAfterStorageFreesSpace()
    {
        var settings = new JobbyServerSettings
        {
            DeleteCompleted = false,
            MaxDegreeOfParallelism = 2
        };
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storageCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completedIds = new List<Guid>();
        _storageMock.Reset();
        _storageMock
            .Setup(x => x.BulkUpdateProcessingJobsToCompletedAsync(It.IsAny<CompleteJobsBatch>()))
            .Returns(async (CompleteJobsBatch batch) =>
            {
                completedIds.AddRange(batch.JobIds);
                storageCalled.TrySetResult();
                await gate.Task;
            });

        var service = new BatchingJobCompletionService(_storageMock.Object, settings, ServerId);
        try
        {
            var jobs = Enumerable.Range(0, 4)
                .Select(_ => new JobExecutionModel { Id = Guid.NewGuid(), NextJobId = null })
                .ToList();

            var tasks = new List<Task>();
            tasks.Add(service.CompleteJob(jobs[0]));
            await storageCalled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            // The reader is blocked in storage; channel capacity = 2.
            // jobs[1] and jobs[2] fill the channel, jobs[3] spins in TryWrite
            // until storage completes and the reader frees a slot.
            tasks.Add(service.CompleteJob(jobs[1]));
            tasks.Add(service.CompleteJob(jobs[2]));
            var overflowingCall = Task.Run(() => service.CompleteJob(jobs[3]), TestContext.Current.CancellationToken);

            await Task.Delay(200, TestContext.Current.CancellationToken);
            Assert.False(overflowingCall.IsCompleted, "CompleteJob must wait when the channel is full (FullMode.Wait)");

            gate.TrySetResult();
            await overflowingCall;
            await Task.WhenAll(tasks);

            Assert.Equal(jobs.Select(j => j.Id).OrderBy(x => x), completedIds.OrderBy(x => x));
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task CompleteJob_JobsWrittenBeforeDispose_AreStillProcessed()
    {
        var settings = new JobbyServerSettings
        {
            DeleteCompleted = false,
            MaxDegreeOfParallelism = 2
        };
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storageCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _storageMock.Reset();
        var completedIds = new List<Guid>();
        _storageMock
            .Setup(x => x.BulkUpdateProcessingJobsToCompletedAsync(It.IsAny<CompleteJobsBatch>()))
            .Returns(async (CompleteJobsBatch batch) =>
            {
                completedIds.AddRange(batch.JobIds);
                storageCalled.TrySetResult();
                await gate.Task;
            });

        var service = new BatchingJobCompletionService(_storageMock.Object, settings, ServerId);
        try
        {
            var job1 = new JobExecutionModel { Id = Guid.NewGuid(), NextJobId = null };
            var job2 = new JobExecutionModel { Id = Guid.NewGuid(), NextJobId = null };

            var task1 = service.CompleteJob(job1);
            await storageCalled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            // Buffered while the reader is blocked in storage
            var task2 = service.CompleteJob(job2);

            service.Dispose();
            gate.TrySetResult();

            await Task.WhenAll(task1, task2);

            Assert.Equal(new[] { job1.Id, job2.Id }.OrderBy(x => x), completedIds.OrderBy(x => x));
        }
        finally
        {
            // Dispose is not idempotent: a second Complete() on the writer throws
            // ChannelClosedException, so the service was already disposed above
        }
    }

    [Fact]
    public async Task CompleteJob_AfterDispose_NeverReturns()
    {
        // Pins the known rough spot: CompleteJob busy-waits in TryWrite
        // without any cancellation, so after Dispose it spins forever
        var settings = new JobbyServerSettings { MaxDegreeOfParallelism = 2 };
        var service = new BatchingJobCompletionService(_storageMock.Object, settings, ServerId);
        service.Dispose();

        var job = new JobExecutionModel { Id = Guid.NewGuid() };

        await Assert.ThrowsAsync<TimeoutException>(() =>
            Task.Run(() => service.CompleteJob(job), TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
    }
}
