using Jobby.Core.Models;
using Jobby.Core.Services;

namespace Jobby.Core.Interfaces;

public interface IJobsFactory
{
    JobCreationModel Create<TCommand>(TCommand command) where TCommand : IJobCommand;
    JobCreationModel Create<TCommand>(TCommand command, string sequenceId) where TCommand : IJobCommand;
    JobCreationModel Create<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand;

    JobCreationModel CreateRecurrent<TCommand>(TCommand command, string cron) where TCommand : IJobCommand;

    JobsSequenceBuilder CreateSequenceBuilder();
    JobsSequenceBuilder CreateSequenceBuilder(int capacity);
}
