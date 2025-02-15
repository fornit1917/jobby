namespace Jobby.Abstractions.Models;

public interface IJobCommandHandler<TCommand> where TCommand : IJobCommand
{
    Task ExecuteAsync(TCommand command);
}
