using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Core.Services;

internal class JobsClient : IJobsClient
{
    private readonly IJobsFactory _jobFactory;
    private readonly IJobbyStorage _jobsStorage;

    public JobsClient(IJobsFactory jobFactory, IJobbyStorage jobsStorage)
    {
        _jobFactory = jobFactory;
        _jobsStorage = jobsStorage;
    }

    public IJobsFactory Factory => _jobFactory;

    public void EnqueueBatch(IReadOnlyList<Job> jobs)
    {
        _jobsStorage.BulkInsert(jobs);
    }

    public Task EnqueueBatchAsync(IReadOnlyList<Job> jobs)
    {
        return _jobsStorage.BulkInsertAsync(jobs);
    }

    public void EnqueueCommand<TCommand>(TCommand command) where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command);
        _jobsStorage.Insert(job);
    }

    public void EnqueueCommand<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command, startTime);
        _jobsStorage.Insert(job);
    }

    public Task EnqueueCommandAsync<TCommand>(TCommand command) where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command);
        return _jobsStorage.InsertAsync(job);
    }

    public Task EnqueueCommandAsync<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command, startTime);
        return _jobsStorage.InsertAsync(job);
    }
}
