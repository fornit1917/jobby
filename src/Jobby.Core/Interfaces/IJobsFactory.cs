using Jobby.Core.Models;
using Jobby.Core.Services;

namespace Jobby.Core.Interfaces;

public interface IJobsFactory
{
    Job Create<TCommand>(TCommand command) where TCommand : IJobCommand;
    Job Create<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand;

    Job CreateRecurrent<TCommand>(TCommand command, string cron) where TCommand : IJobCommand;

    JobsSequenceBuilder CreateSequenceBuilder();
    JobsSequenceBuilder CreateSequenceBuilder(int capacity);
}
