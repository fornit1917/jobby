using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobsFactory
{
    Job Create<TCommand>(TCommand command) where TCommand : IJobCommand;
    Job Create<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand;
}
