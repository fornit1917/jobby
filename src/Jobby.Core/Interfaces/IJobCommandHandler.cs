using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobCommandHandler<TCommand> where TCommand : IJobCommand
{
    Task ExecuteAsync(TCommand command, CommandExecutionContext ctx);
}
