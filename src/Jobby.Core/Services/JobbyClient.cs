using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;
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

    public Task CancelRecurrentByIdsAsync(params Guid[] ids)
    {
        return _storage.BulkDeleteRecurrentAsync(ids);
    }

    public void CancelRecurrentByIds(params Guid[] ids)
    {
        _storage.BulkDeleteRecurrent(ids);
    }

    public Task CancelJobsByIdsAsync(params Guid[] ids)
    {
        return _storage.BulkDeleteNotStartedJobsAsync(ids);
    }

    public void CancelRecurrent<TCommand>() where TCommand : IJobCommand
    {
        _storage.DeleteExclusiveByName(TCommand.GetJobName());
    }

    public Task CancelRecurrentAsync<TCommand>() where TCommand : IJobCommand
    {
        return _storage.DeleteExclusiveByNameAsync(TCommand.GetJobName());
    }

    public void EnqueueBatch(IReadOnlyList<JobCreationModel> jobs)
    {
        _storage.BulkInsertJobs(jobs);
    }

    public Task EnqueueBatchAsync(IReadOnlyList<JobCreationModel> jobs)
    {
        return _storage.BulkInsertJobsAsync(jobs);
    }

    public Guid EnqueueCommand<TCommand>(TCommand command, JobOpts opts = default)
        where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command, opts);
        return _storage.InsertJob(job);
    }

    public Guid EnqueueCommand<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command, startTime);
        return _storage.InsertJob(job);
    }

    public Task<Guid> EnqueueCommandAsync<TCommand>(TCommand command, JobOpts opts = default)
        where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command, opts);
        return _storage.InsertJobAsync(job);
    }

    public Task<Guid> EnqueueCommandAsync<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command, startTime);
        return _storage.InsertJobAsync(job);
    }
    
    public Task<Guid> ScheduleRecurrentAsync<TCommand>(TCommand command,
        string cron,
        RecurrentJobOpts opts = default) where TCommand : IJobCommand
    {
        var job = _jobFactory.CreateRecurrent(command, cron, opts);
        return _storage.InsertJobAsync(job);
    }

    public Task<Guid> ScheduleRecurrentAsync<TCommand, TSchedule>(TCommand command,
        TSchedule schedule,
        RecurrentJobOpts opts = default)
            where TCommand : IJobCommand
            where TSchedule : ISchedule
    {
        var job = _jobFactory.CreateRecurrent(command, schedule, opts);
        return _storage.InsertJobAsync(job);
    }

    public Guid ScheduleRecurrent<TCommand>(TCommand command,
        string cron,
        RecurrentJobOpts opts = default) where TCommand : IJobCommand
    {
        var job = _jobFactory.CreateRecurrent(command, cron, opts);
        return _storage.InsertJob(job);
    }
    
    public Guid ScheduleRecurrent<TCommand, TSchedule>(TCommand command, 
        TSchedule schedule,
        RecurrentJobOpts opts = default) 
            where TCommand : IJobCommand
            where TSchedule : ISchedule
    {
        var job = _jobFactory.CreateRecurrent(command, schedule, opts);
        return _storage.InsertJob(job);
    }
}
