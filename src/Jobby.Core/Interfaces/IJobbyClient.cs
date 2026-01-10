using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobbyClient
{
    public IJobsFactory Factory { get; }

    Task<Guid> EnqueueCommandAsync<TCommand>(TCommand command) where TCommand : IJobCommand;
    Task<Guid> EnqueueCommandAsync<TCommand>(TCommand command, string sequenceId) where TCommand : IJobCommand;
    Task<Guid> EnqueueCommandAsync<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand;
    Task<Guid> EnqueueCommandAsync<TCommand>(TCommand command, JobCreationOptions options) where TCommand : IJobCommand;
    Guid EnqueueCommand<TCommand>(TCommand command) where TCommand : IJobCommand;
    Guid EnqueueCommand<TCommand>(TCommand command, string sequenceId) where TCommand : IJobCommand;
    Guid EnqueueCommand<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand;
    Guid EnqueueCommand<TCommand>(TCommand command, JobCreationOptions options) where TCommand : IJobCommand;

    Task CancelJobsByIdsAsync(params Guid[] ids);
    void CancelJobsByIds(params Guid[] ids);

    Task EnqueueBatchAsync(IReadOnlyList<JobCreationModel> jobs);
    void EnqueueBatch(IReadOnlyList<JobCreationModel> jobs);

    Task ScheduleRecurrentAsync<TCommand>(TCommand command, string cron) where TCommand : IJobCommand;
    void ScheduleRecurrent<TCommand>(TCommand command, string cron) where TCommand : IJobCommand;

    Task CancelRecurrentAsync<TCommand>() where TCommand : IJobCommand;
    void CancelRecurrent<TCommand>() where TCommand : IJobCommand;
}
