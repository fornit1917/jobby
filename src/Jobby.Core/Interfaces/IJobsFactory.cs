using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models;
using Jobby.Core.Services;

namespace Jobby.Core.Interfaces;

public interface IJobsFactory
{
    JobCreationModel Create<TCommand>(TCommand command, JobOpts opts = default) where TCommand : IJobCommand;
    JobCreationModel Create<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand;
       
    JobCreationModel CreateRecurrent<TCommand, TScheduler>(TCommand command,
        TScheduler schedule,
        RecurrentJobOpts opts = default
    )
        where TCommand : IJobCommand
        where TScheduler : IScheduler;

    JobsSequenceBuilder CreateSequenceBuilder();
    JobsSequenceBuilder CreateSequenceBuilder(int capacity);
}
