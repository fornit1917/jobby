using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models;

namespace Jobby.Tests.Dashboard;

// The Jobby client without a database: jobs are built by the real factory and kept in memory.
internal sealed class InMemoryJobbyClient : IJobbyClient
{
    public InMemoryJobbyClient(IJobsFactory factory) => Factory = factory;

    public IJobsFactory Factory { get; }

    public List<JobCreationModel> Jobs { get; } = [];

    public Task<Guid> EnqueueCommandAsync<TCommand>(TCommand command, JobOpts opts = default)
        where TCommand : IJobCommand
        => Task.FromResult(Add(Factory.Create(command, opts)));

    public Task<Guid> ScheduleRecurrentAsync<TCommand>(TCommand command, string cron,
        RecurrentJobOpts opts = default) where TCommand : IJobCommand
        => Task.FromResult(Add(Factory.CreateRecurrent(command, cron, opts)));

    private Guid Add(JobCreationModel job)
    {
        Jobs.Add(job);
        return job.Id;
    }

    public Task<Guid> EnqueueCommandAsync<TCommand>(TCommand command, DateTime startTime)
        where TCommand : IJobCommand => throw new NotSupportedException();

    public Guid EnqueueCommand<TCommand>(TCommand command, JobOpts opts = default)
        where TCommand : IJobCommand => throw new NotSupportedException();

    public Guid EnqueueCommand<TCommand>(TCommand command, DateTime startTime)
        where TCommand : IJobCommand => throw new NotSupportedException();

    public Task EnqueueBatchAsync(IReadOnlyList<JobCreationModel> jobs) => throw new NotSupportedException();

    public void EnqueueBatch(IReadOnlyList<JobCreationModel> jobs) => throw new NotSupportedException();

    public Task<Guid> ScheduleRecurrentAsync<TCommand, TSchedule>(TCommand command, TSchedule schedule,
        RecurrentJobOpts opts = default)
        where TCommand : IJobCommand
        where TSchedule : ISchedule => throw new NotSupportedException();

    public Guid ScheduleRecurrent<TCommand>(TCommand command, string cron, RecurrentJobOpts opts = default)
        where TCommand : IJobCommand => throw new NotSupportedException();

    public Guid ScheduleRecurrent<TCommand, TSchedule>(TCommand command, TSchedule schedule,
        RecurrentJobOpts opts = default)
        where TCommand : IJobCommand
        where TSchedule : ISchedule => throw new NotSupportedException();

    public Task CancelRecurrentAsync<TCommand>() where TCommand : IJobCommand => throw new NotSupportedException();

    public void CancelRecurrent<TCommand>() where TCommand : IJobCommand => throw new NotSupportedException();

    public Task CancelRecurrentByIdsAsync(params Guid[] ids) => throw new NotSupportedException();

    public void CancelRecurrentByIds(params Guid[] ids) => throw new NotSupportedException();

    public Task CancelJobsByIdsAsync(params Guid[] ids) => throw new NotSupportedException();

    public void CancelJobsByIds(params Guid[] ids) => throw new NotSupportedException();
}
