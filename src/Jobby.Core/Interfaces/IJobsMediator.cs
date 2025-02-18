namespace Jobby.Core.Interfaces;

public interface IJobsMediator
{
    Task EnqueueCommandAsync<TCommand>(TCommand command) where TCommand : IJobCommand;
    void EnqueueCommand<TCommand>(TCommand command) where TCommand : IJobCommand;
}
