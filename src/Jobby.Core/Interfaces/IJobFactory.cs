using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobFactory
{
    JobModel Create<TCommand>(TCommand command) where TCommand : IJobCommand;
    JobModel Create<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand;
}
