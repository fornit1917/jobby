using Jobby.Core.Models;
using Jobby.Core.Services;

namespace Jobby.Core.Interfaces;

public interface IJobsFactory
{
    JobCreationModel Create<TCommand>(TCommand command, JobOpts opts = default) where TCommand : IJobCommand;
    JobCreationModel Create<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand;
    
    JobCreationModel CreateRecurrent<TCommand>(TCommand command, 
        string cron,
        RecurrentJobOpts opts = default) where TCommand : IJobCommand;
    
    JobCreationModel CreateRecurrent<TCommand>(TCommand command,
        string schedule,
        string schedulerType,
        RecurrentJobOpts opts = default) where TCommand : IJobCommand;

    JobsSequenceBuilder CreateSequenceBuilder();
    JobsSequenceBuilder CreateSequenceBuilder(int capacity);
}
