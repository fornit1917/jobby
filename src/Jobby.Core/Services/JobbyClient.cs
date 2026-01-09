using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Core.Services;

internal class JobbyClient : IJobbyClient
{
    private readonly IJobsFactory _jobFactory;
    private readonly IJobbyStorage _storage;

    public JobbyClient(IJobsFactory jobFactory, IJobbyStorage jobsStorage)
    {
        _jobFactory = jobFactory;
        _storage = jobsStorage;
    }

    public IJobsFactory Factory => _jobFactory;

    public void CancelJobsByIds(params Guid[] ids)
    {
        _storage.BulkDeleteNotStartedJobs(ids);
    }

    public Task CancelJobsByIdsAsync(params Guid[] ids)
    {
        return _storage.BulkDeleteNotStartedJobsAsync(ids);
    }

    public void CancelRecurrent<TCommand>() where TCommand : IJobCommand
    {
        _storage.DeleteRecurrent(TCommand.GetJobName());
    }

    public Task CancelRecurrentAsync<TCommand>() where TCommand : IJobCommand
    {
        return _storage.DeleteRecurrentAsync(TCommand.GetJobName());
    }

    public void EnqueueBatch(IReadOnlyList<JobCreationModel> jobs)
    {
        _storage.BulkInsertJobs(jobs);
    }

    public Task EnqueueBatchAsync(IReadOnlyList<JobCreationModel> jobs)
    {
        return _storage.BulkInsertJobsAsync(jobs);
    }

    public Guid EnqueueCommand<TCommand>(TCommand command) where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command);
        _storage.InsertJob(job);
        return job.Id;
    }

    public Guid EnqueueCommand<TCommand>(TCommand command, string sequenceId) where TCommand : IJobCommand
    {
        ArgumentNullException.ThrowIfNull(sequenceId);
        var job = _jobFactory.Create(command, sequenceId);
        _storage.InsertJob(job);
        return job.Id;
    }

    public Guid EnqueueCommand<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command, startTime);
        _storage.InsertJob(job);
        return job.Id;
    }

    public async Task<Guid> EnqueueCommandAsync<TCommand>(TCommand command) where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command);
        await _storage.InsertJobAsync(job);
        return job.Id;
    }

    public async Task<Guid> EnqueueCommandAsync<TCommand>(TCommand command, string sequenceId) where TCommand : IJobCommand
    {
        ArgumentNullException.ThrowIfNull(sequenceId);

        var job = _jobFactory.Create(command, sequenceId);
        await _storage.InsertJobAsync(job);
        return job.Id;
    }

    public async Task<Guid> EnqueueCommandAsync<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command, startTime);
        await _storage.InsertJobAsync(job);
        return job.Id;
    }

    public void ScheduleRecurrent<TCommand>(TCommand command, string cron) where TCommand : IJobCommand
    {
        var job = _jobFactory.CreateRecurrent(command, cron);
        _storage.InsertJob(job);
    }

    public Task ScheduleRecurrentAsync<TCommand>(TCommand command, string cron) where TCommand : IJobCommand
    {
        var job = _jobFactory.CreateRecurrent(command, cron);
        return _storage.InsertJobAsync(job);
    }
}
