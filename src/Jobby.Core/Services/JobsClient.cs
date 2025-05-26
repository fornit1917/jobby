using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Core.Services;

internal class JobsClient : IJobsClient
{
    private readonly IJobsFactory _jobFactory;
    private readonly IJobbyStorage _storage;

    public JobsClient(IJobsFactory jobFactory, IJobbyStorage jobsStorage)
    {
        _jobFactory = jobFactory;
        _storage = jobsStorage;
    }

    public IJobsFactory Factory => _jobFactory;

    public void EnqueueBatch(IReadOnlyList<Job> jobs)
    {
        _storage.BulkInsert(jobs);
    }

    public Task EnqueueBatchAsync(IReadOnlyList<Job> jobs)
    {
        return _storage.BulkInsertAsync(jobs);
    }

    public void EnqueueCommand<TCommand>(TCommand command) where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command);
        _storage.Insert(job);
    }

    public void EnqueueCommand<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command, startTime);
        _storage.Insert(job);
    }

    public Task EnqueueCommandAsync<TCommand>(TCommand command) where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command);
        return _storage.InsertAsync(job);
    }

    public Task EnqueueCommandAsync<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command, startTime);
        return _storage.InsertAsync(job);
    }

    public void ScheduleRecurrent<TCommand>(TCommand command, string cron) where TCommand : IJobCommand
    {
        var job = _jobFactory.CreateRecurrent(command, cron);
        _storage.Insert(job);
    }

    public Task ScheduleRecurrentAsync<TCommand>(TCommand command, string cron) where TCommand : IJobCommand
    {
        var job = _jobFactory.CreateRecurrent(command, cron);
        return _storage.InsertAsync(job);
    }
}
