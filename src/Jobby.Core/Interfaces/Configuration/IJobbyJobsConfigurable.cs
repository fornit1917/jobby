using System.Reflection;

namespace Jobby.Core.Interfaces.Configuration;

public interface IJobbyJobsConfigurable
{
    IJobbyJobsConfigurable AddJob<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>;

    IJobbyJobsConfigurable AddOrReplaceJob<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>;

    IJobbyJobsConfigurable AddJobsFromAssemblies(params Assembly[] assemblies);
    
    IJobbyJobsConfigurable UseQueueForJob<TCommand>(string queueName) where TCommand : IJobCommand;
    
    IJobbyJobsConfigurable UseQueueForAllRecurrent(string queueName);
}
