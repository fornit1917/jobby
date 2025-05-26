using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobsClient
{
    public IJobsFactory Factory { get; }

    Task EnqueueCommandAsync<TCommand>(TCommand command) where TCommand : IJobCommand;
    Task EnqueueCommandAsync<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand;
    void EnqueueCommand<TCommand>(TCommand command) where TCommand : IJobCommand;
    void EnqueueCommand<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand;

    Task EnqueueBatchAsync(IReadOnlyList<JobCreationModel> jobs);
    void EnqueueBatch(IReadOnlyList<JobCreationModel> jobs);

    Task ScheduleRecurrentAsync<TCommand>(TCommand command, string cron) where TCommand : IJobCommand;
    void ScheduleRecurrent<TCommand>(TCommand command, string cron) where TCommand : IJobCommand;
}
