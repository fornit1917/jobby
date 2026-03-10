using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models;
using Jobby.Core.Services.Schedulers.CronSimple;

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
        _storage.InsertJob(job);
        return job.Id;
    }

    public Guid EnqueueCommand<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command, startTime);
        _storage.InsertJob(job);
        return job.Id;
    }

    public async Task<Guid> EnqueueCommandAsync<TCommand>(TCommand command, JobOpts opts = default)
        where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command, opts);
        await _storage.InsertJobAsync(job);
        return job.Id;
    }

    public async Task<Guid> EnqueueCommandAsync<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand
    {
        var job = _jobFactory.Create(command, startTime);
        await _storage.InsertJobAsync(job);
        return job.Id;
    }

    public async Task<Guid> ScheduleRecurrentAsync<TCommand>(TCommand command,
        string cron,
        bool calculateNextFromPrev = false,
        RecurrentJobOpts opts = default) where TCommand : IJobCommand
    {
        var cronExpression = CronHelper.Parse(cron);

        var job = _jobFactory.CreateRecurrent(command, new CronSimpleScheduler(cronExpression), opts);
        await _storage.InsertJobAsync(job);
        return job.Id;
    }

    public async Task<Guid> ScheduleRecurrentAsync<TCommand, TScheduler>(TCommand command, TScheduler schedule,
        RecurrentJobOpts opts = default)
        where TCommand : IJobCommand
        where TScheduler : IScheduler
    {
        var job = _jobFactory.CreateRecurrent(command, schedule, opts);
        await _storage.InsertJobAsync(job);
        return job.Id;
    }

    public Guid ScheduleRecurrent<TCommand>(TCommand command,
        string cron,
        RecurrentJobOpts opts = default) where TCommand : IJobCommand
    {
        if (!CronHelper.TryParse(cron, out var cronExpression))
            throw new ArgumentException($"{nameof(cron)} has invalid cron format: {cron}");

        var job = _jobFactory.CreateRecurrent(command, new CronSimpleScheduler(cronExpression), opts);
        _storage.InsertJob(job);
        return job.Id;
    }
    
    public Guid ScheduleRecurrent<TCommand, TScheduler>(TCommand command, TScheduler schedule,
        RecurrentJobOpts opts = default)
        where TCommand : IJobCommand
        where TScheduler : IScheduler
    {
        var job = _jobFactory.CreateRecurrent(command, schedule, opts);
        _storage.InsertJob(job);
        return job.Id;
    }
}
