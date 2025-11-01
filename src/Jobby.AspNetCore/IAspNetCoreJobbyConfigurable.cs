using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Configuration;
using System.Reflection;

namespace Jobby.AspNetCore;

public interface IAspNetCoreJobbyConfigurable
{
    IAspNetCoreJobbyConfigurable ConfigureJobby(Action<IJobbyComponentsConfigurable> configure);
    IAspNetCoreJobbyConfigurable ConfigureJobby(Action<IServiceProvider, IJobbyComponentsConfigurable> configure);

    IAspNetCoreJobbyConfigurable AddJob<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>;

    IAspNetCoreJobbyConfigurable AddOrReplaceJob<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>;

    IAspNetCoreJobbyConfigurable AddJobsFromAssemblies(params Assembly[] assemblies);
}
