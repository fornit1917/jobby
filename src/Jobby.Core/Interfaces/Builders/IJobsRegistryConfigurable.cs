using System.Reflection;

namespace Jobby.Core.Interfaces.Builders;

public interface IJobsRegistryConfigurable
{
    IJobsRegistryConfigurable AddJob<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>;

    IJobsRegistryConfigurable AddJobsFromAssemblies(params Assembly[] assemblies);
}
