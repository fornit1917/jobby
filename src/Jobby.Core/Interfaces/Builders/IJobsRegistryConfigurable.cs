using System.Reflection;

namespace Jobby.Core.Interfaces.Builders;

public interface IJobsRegistryConfigurable
{
    IJobsRegistryConfigurable AddCommand<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>;

    IJobsRegistryConfigurable AddRecurrentJob<THandler>() where THandler : IRecurrentJobHandler;

    IJobsRegistryConfigurable AddJobsFromAssemblies(params Assembly[] assemblies);
}
