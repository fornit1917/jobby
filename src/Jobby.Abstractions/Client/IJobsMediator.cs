using Jobby.Abstractions.Models;

namespace Jobby.Abstractions.Client;

public interface IJobsMediator
{
    Task EnqueueCommandAsync<TCommand>(TCommand command) where TCommand : IJobCommand;
    void EnqueueCommand<TCommand>(TCommand command) where TCommand: IJobCommand;
}
