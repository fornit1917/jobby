namespace Jobby.Core.Interfaces;

public interface IJobsClient
{
    Task EnqueueCommandAsync<TCommand>(TCommand command) where TCommand : IJobCommand;
    Task EnqueueCommandAsync<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand;

    void EnqueueCommand<TCommand>(TCommand command) where TCommand : IJobCommand;
    void EnqueueCommand<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand;
}
