using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobbyClient
{
    public IJobsFactory Factory { get; }

    Task<Guid> EnqueueCommandAsync<TCommand>(TCommand command, JobOpts opts = default) where TCommand : IJobCommand;
    Task<Guid> EnqueueCommandAsync<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand;
    
    Guid EnqueueCommand<TCommand>(TCommand command, JobOpts opts = default) where TCommand : IJobCommand;
    Guid EnqueueCommand<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand;

    Task EnqueueBatchAsync(IReadOnlyList<JobCreationModel> jobs);
    void EnqueueBatch(IReadOnlyList<JobCreationModel> jobs);

    Task<Guid> ScheduleRecurrentAsync<TCommand>(TCommand command,
        string cron,
        RecurrentJobOpts opts = default) where TCommand : IJobCommand;

    Task<Guid> ScheduleRecurrentAsync<TCommand, TScheduler>(TCommand command, TScheduler schedule,
        RecurrentJobOpts opts = default)
        where TCommand : IJobCommand
        where TScheduler : IScheduler;

    Guid ScheduleRecurrent<TCommand>(TCommand command, 
        string cron,
        RecurrentJobOpts opts = default) where TCommand : IJobCommand;
    
    Guid ScheduleRecurrent<TCommand, TScheduler>(TCommand command, TScheduler schedule,
        RecurrentJobOpts opts = default)
        where TCommand : IJobCommand
        where TScheduler : IScheduler;    

    Task CancelRecurrentAsync<TCommand>() where TCommand : IJobCommand;
    void CancelRecurrent<TCommand>() where TCommand : IJobCommand;
    
    Task CancelRecurrentByIdsAsync(params Guid[] ids);
    void CancelRecurrentByIds(params Guid[] ids);
    
    Task CancelJobsByIdsAsync(params Guid[] ids);
    void CancelJobsByIds(params Guid[] ids);
}
