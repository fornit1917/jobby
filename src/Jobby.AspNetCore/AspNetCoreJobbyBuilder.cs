using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Configuration;
using Jobby.Core.Services;
using System.Reflection;

namespace Jobby.AspNetCore;

internal class AspNetCoreJobbyBuilder : IAspNetCoreJobbyConfigurable
{
    public JobbyBuilder JobbyBuilder { get; } = new JobbyBuilder();

    private Action<IServiceProvider, IJobbyComponentsConfigurable>? _jobbyComponentsConfigureAction;

    public IAspNetCoreJobbyConfigurable AddJob<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>
    {
        JobbyBuilder.AddJob<TCommand, THandler>();
        return this;
    }

    public IAspNetCoreJobbyConfigurable AddJobsFromAssemblies(params Assembly[] assemblies)
    {
        JobbyBuilder.AddJobsFromAssemblies(assemblies);
        return this;
    }

    public IAspNetCoreJobbyConfigurable AddOrReplaceJob<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>
    {
        JobbyBuilder.AddOrReplaceJob<TCommand, THandler>();
        return this;
    }

    public IAspNetCoreJobbyConfigurable ConfigureJobby(Action<IJobbyComponentsConfigurable> configure)
    {
        configure(JobbyBuilder);
        return this;
    }

    public IAspNetCoreJobbyConfigurable ConfigureJobby(Action<IServiceProvider, IJobbyComponentsConfigurable> configure)
    {
        _jobbyComponentsConfigureAction = configure;
        return this;
    }

    public void ApplyJobbyComponentsConfigureAction(IServiceProvider sp)
    {
        _jobbyComponentsConfigureAction?.Invoke(sp, JobbyBuilder);
    }
}
