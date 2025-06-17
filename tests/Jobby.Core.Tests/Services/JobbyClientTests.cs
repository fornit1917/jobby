using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Moq;

namespace Jobby.Core.Tests.Services;

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
    public void CancelJobsByIds_CallsBulkDelete()
    {
        Guid[] ids = [ Guid.NewGuid() ];
        _client.CancelJobsByIds(ids);
        _storageMock.Verify(x => x.BulkDelete(ids));
    }

    [Fact]
    public async Task CancelJobsByIdsAsync_CallsBulkDeleteAsync()
    {
        Guid[] ids = [ Guid.NewGuid() ];
        await _client.CancelJobsByIdsAsync(ids);
        _storageMock.Verify(x => x.BulkDeleteAsync(ids, null));
    }

    [Fact]
    public void CancelRecurrent_CallsDeleteRecurrent()
    {
        _client.CancelRecurrent<TestJobCommand>();
        _storageMock.Verify(x => x.DeleteRecurrent(TestJobCommand.GetJobName()));
    }

    [Fact]
    public async Task CancelRecurrentAsync_CallsDeleteRecurrentAsync()
    {
        await _client.CancelRecurrentAsync<TestJobCommand>();
        _storageMock.Verify(x => x.DeleteRecurrentAsync(TestJobCommand.GetJobName()));
    }

    [Fact]
    public void EnqueueBatch_CallsBulkInsert()
    {
        var jobs = new List<JobCreationModel>()
        {
            new JobCreationModel()
        };

        _client.EnqueueBatch(jobs);

        _storageMock.Verify(x => x.BulkInsert(jobs));
    }

    [Fact]
    public async Task EnqueueBatchAsync_CallsBulkInsertAsync()
    {
        var jobs = new List<JobCreationModel>()
        {
            new JobCreationModel()
        };

        await _client.EnqueueBatchAsync(jobs);

        _storageMock.Verify(x => x.BulkInsertAsync(jobs));
    }

    [Fact]
    public void EnqueueCommand_TimeNotSpecified_CreatesAndInsertsJobAndReturnsId()
    {
        var command = new TestJobCommand();
        var job = new JobCreationModel { Id = Guid.NewGuid() };
        _factoryMock.Setup(x => x.Create(command)).Returns(job);

        var id = _client.EnqueueCommand(command);

        _storageMock.Verify(x => x.Insert(job));
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

        _storageMock.Verify(x => x.Insert(job));
        Assert.Equal(job.Id, id);
    }

    [Fact]
    public async Task EnqueueCommandAsync_TimeNotSpecified_CreatesAndInsertsJobAndReturnsId()
    {
        var command = new TestJobCommand();
        var job = new JobCreationModel { Id = Guid.NewGuid() };
        _factoryMock.Setup(x => x.Create(command)).Returns(job);

        var id = await _client.EnqueueCommandAsync(command);

        _storageMock.Verify(x => x.InsertAsync(job));
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

        _storageMock.Verify(x => x.InsertAsync(job));
        Assert.Equal(job.Id, id);
    }

    [Fact]
    public void ScheduleRecurrent_CreatesAndInsertsJobWithSpecifiedCronAndReturnsId()
    {
        var command = new TestJobCommand();
        var cron = "*/3 * * * * *";
        var job = new JobCreationModel { Id = Guid.NewGuid() };
        _factoryMock.Setup(x => x.CreateRecurrent(command, cron)).Returns(job);

        var id = _client.ScheduleRecurrent(command, cron);

        _storageMock.Verify(x => x.Insert(job));
        Assert.Equal(job.Id, id);
    }

    [Fact]
    public async Task ScheduleRecurrentAsync_CreatesAndInsertsJobWithSpecifiedCronAndReturnsId()
    {
        var command = new TestJobCommand();
        var cron = "*/3 * * * * *";
        var job = new JobCreationModel { Id = Guid.NewGuid() };
        _factoryMock.Setup(x => x.CreateRecurrent(command, cron)).Returns(job);

        var id = await _client.ScheduleRecurrentAsync(command, cron);

        _storageMock.Verify(x => x.InsertAsync(job));
        Assert.Equal(job.Id, id);
    }

    private class TestJobCommand : IJobCommand
    {
        public static string GetJobName() => "TestJobName";
        public bool CanBeRestarted() => false;
    }
}
