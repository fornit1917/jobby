using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.TestsUtils.Jobs;
using Moq;

namespace Jobby.Tests.Core.Services;

public class JobbyClientTests
{
    private readonly Mock<IJobsFactory> _factoryMock;
    private readonly Mock<IJobbyStorage> _storageMock;

    private readonly IJobbyClient _client;

    public JobbyClientTests()
    {
        _factoryMock = new Mock<IJobsFactory>();
        _storageMock = new Mock<IJobbyStorage>();
        _client = new JobbyClient(_factoryMock.Object, _storageMock.Object);
    }

    [Fact]
    public void CancelJobsByIds_CallsBulkDeleteNotStarted()
    {
        Guid[] ids = [Guid.NewGuid()];
        _client.CancelJobsByIds(ids);
        _storageMock.Verify(x => x.BulkDeleteNotStartedJobs(ids));
    }

    [Fact]
    public async Task CancelJobsByIdsAsync_CallsBulkDeleteNotStartedAsync()
    {
        Guid[] ids = [Guid.NewGuid()];
        await _client.CancelJobsByIdsAsync(ids);
        _storageMock.Verify(x => x.BulkDeleteNotStartedJobsAsync(ids));
    }

    [Fact]
    public void CancelRecurrent_CallsDeleteRecurrent()
    {
        _client.CancelRecurrent<TestJobCommand>();
        _storageMock.Verify(x => x.DeleteExclusiveByName(TestJobCommand.GetJobName()));
    }

    [Fact]
    public async Task CancelRecurrentAsync_CallsDeleteRecurrentAsync()
    {
        await _client.CancelRecurrentAsync<TestJobCommand>();
        _storageMock.Verify(x => x.DeleteExclusiveByNameAsync(TestJobCommand.GetJobName()));
    }

    [Fact]
    public void EnqueueBatch_CallsBulkInsert()
    {
        var jobs = new List<JobCreationModel>()
        {
            new JobCreationModel()
        };

        _client.EnqueueBatch(jobs);

        _storageMock.Verify(x => x.BulkInsertJobs(jobs));
    }

    [Fact]
    public async Task EnqueueBatchAsync_CallsBulkInsertAsync()
    {
        var jobs = new List<JobCreationModel>()
        {
            new JobCreationModel()
        };

        await _client.EnqueueBatchAsync(jobs);

        _storageMock.Verify(x => x.BulkInsertJobsAsync(jobs));
    }

    [Fact]
    public void EnqueueCommand_OptionsNotSpecified_CreatesAndInsertsJobAndReturnsId()
    {
        var command = new TestJobCommand();
        var job = new JobCreationModel { Id = Guid.NewGuid() };
        _factoryMock.Setup(x => x.Create(command, default(JobOpts))).Returns(job);

        var id = _client.EnqueueCommand(command);

        _storageMock.Verify(x => x.InsertJob(job));
        Assert.Equal(job.Id, id);
    }
    
    [Fact]
    public void EnqueueCommand_OptionsSpecified_CreatesAndInsertsJobAndReturnsId()
    {
        var command = new TestJobCommand();
        var job = new JobCreationModel { Id = Guid.NewGuid() };
        var opts = new JobOpts { QueueName = "q" };
        _factoryMock.Setup(x => x.Create(command, opts)).Returns(job);

        var id = _client.EnqueueCommand(command, opts);

        _storageMock.Verify(x => x.InsertJob(job));
        Assert.Equal(job.Id, id);
    }

    [Fact]
    public void EnqueueCommand_TimeSpecified_CreatesAndInsertsJobAndReturnsId()
    {
        var command = new TestJobCommand();
        var job = new JobCreationModel { Id = Guid.NewGuid() };
        var startTime = DateTime.UtcNow;
        _factoryMock.Setup(x => x.Create(command, startTime)).Returns(job);

        var id = _client.EnqueueCommand(command, startTime);

        _storageMock.Verify(x => x.InsertJob(job));
        Assert.Equal(job.Id, id);
    }

    [Fact]
    public async Task EnqueueCommandAsync_OptionsNotSpecified_CreatesAndInsertsJobAndReturnsId()
    {
        var command = new TestJobCommand();
        var job = new JobCreationModel { Id = Guid.NewGuid() };
        _factoryMock.Setup(x => x.Create(command, default(JobOpts))).Returns(job);

        var id = await _client.EnqueueCommandAsync(command);

        _storageMock.Verify(x => x.InsertJobAsync(job));
        Assert.Equal(job.Id, id);
    }
    
    [Fact]
    public async Task EnqueueCommandAsync_OptionsSpecified_CreatesAndInsertsJobAndReturnsId()
    {
        var command = new TestJobCommand();
        var job = new JobCreationModel { Id = Guid.NewGuid() };
        var opts = new JobOpts { QueueName = "q" };
        _factoryMock.Setup(x => x.Create(command, opts)).Returns(job);

        var id = await _client.EnqueueCommandAsync(command, opts);

        _storageMock.Verify(x => x.InsertJobAsync(job));
        Assert.Equal(job.Id, id);
    }

    [Fact]
    public async Task EnqueueCommandAsync_TimeSpecified_CreatesAndInsertsJobAndReturnsId()
    {
        var command = new TestJobCommand();
        var job = new JobCreationModel { Id = Guid.NewGuid() };
        var startTime = DateTime.UtcNow;
        _factoryMock.Setup(x => x.Create(command, startTime)).Returns(job);

        var id = await _client.EnqueueCommandAsync(command, startTime);

        _storageMock.Verify(x => x.InsertJobAsync(job));
        Assert.Equal(job.Id, id);
    }

    [Fact]
    public void ScheduleRecurrent_OptionsNotSpecified_CreatesAndInsertsJobWithSpecifiedCron()
    {
        var command = new TestJobCommand();
        var cron = "*/3 * * * * *";
        var job = new JobCreationModel { Id = Guid.NewGuid() };
        _factoryMock.Setup(x => x.CreateRecurrent(command, cron, default)).Returns(job);

        _client.ScheduleRecurrent(command, cron);

        _storageMock.Verify(x => x.InsertJob(job));
    }

    [Fact]
    public async Task ScheduleRecurrentAsync_OptionsNotSpecified_CreatesAndInsertsJobWithSpecifiedCron()
    {
        var command = new TestJobCommand();
        var cron = "*/3 * * * * *";
        var job = new JobCreationModel { Id = Guid.NewGuid() };
        _factoryMock.Setup(x => x.CreateRecurrent(command, cron, default)).Returns(job);

        await _client.ScheduleRecurrentAsync(command, cron);

        _storageMock.Verify(x => x.InsertJobAsync(job));
    }
    
    [Fact]
    public void ScheduleRecurrent_OptionsSpecified_CreatesAndInsertsJobWithSpecifiedCronAndOptions()
    {
        var command = new TestJobCommand();
        var cron = "*/3 * * * * *";
        var opts = new RecurrentJobOpts { QueueName = "q" };
        var job = new JobCreationModel { Id = Guid.NewGuid() };
        _factoryMock.Setup(x => x.CreateRecurrent(command, cron, opts)).Returns(job);

        _client.ScheduleRecurrent(command, cron, opts);

        _storageMock.Verify(x => x.InsertJob(job));
    }

    [Fact]
    public async Task ScheduleRecurrentAsync_OptionsSpecified_CreatesAndInsertsJobWithSpecifiedCronAndOptions()
    {
        var command = new TestJobCommand();
        var cron = "*/3 * * * * *";
        var opts = new RecurrentJobOpts { QueueName = "q" };
        var job = new JobCreationModel { Id = Guid.NewGuid() };
        _factoryMock.Setup(x => x.CreateRecurrent(command, cron, opts)).Returns(job);

        await _client.ScheduleRecurrentAsync(command, cron, opts);

        _storageMock.Verify(x => x.InsertJobAsync(job));
    }

    [Fact]
    public async Task CancelRecurrentByIdsAsync_DeletesSpecifiedRecurrentJobs()
    {
        Guid[] ids = [Guid.NewGuid(),  Guid.NewGuid()];
        
        await _client.CancelRecurrentByIdsAsync(ids);
        
        _storageMock.Verify(x => x.BulkDeleteRecurrentAsync(ids), Times.Once);
    }
    
    [Fact]
    public void CancelRecurrentByIds_DeletesSpecifiedRecurrentJobs()
    {
        Guid[] ids = [Guid.NewGuid(),  Guid.NewGuid()];
        
        _client.CancelRecurrentByIds(ids);
        
        _storageMock.Verify(x => x.BulkDeleteRecurrent(ids), Times.Once);
    }
}
