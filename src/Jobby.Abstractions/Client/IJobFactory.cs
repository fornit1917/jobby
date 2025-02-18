using Jobby.Abstractions.Models;

namespace Jobby.Abstractions.Client;

public interface IJobFactory
{
    JobModel Create<TCommand>(TCommand command) where TCommand : IJobCommand;
    JobModel Create<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand;
}
