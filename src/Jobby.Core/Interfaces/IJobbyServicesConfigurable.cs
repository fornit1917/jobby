using Jobby.Core.Models;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

namespace Jobby.Core.Interfaces;

public interface IJobbyServicesConfigurable
{
    IJobbyServicesConfigurable UseStorage(IJobbyStorage storage);

    IJobbyServicesConfigurable UseLoggerFactory(ILoggerFactory loggerFactory);

    IJobbyServicesConfigurable UseSystemTextJson(JsonSerializerOptions jsonOptions);

    IJobbyServicesConfigurable UseServerSettings(JobbyServerSettings settings);

    IJobbyServicesConfigurable UseExecutionScopeFactory(IJobExecutionScopeFactory scopeFactory);

    IJobbyServicesConfigurable UseDefaultRetryPolicy(RetryPolicy retryPolicy);

    IJobbyServicesConfigurable UseRetryPolicyForJob<TCommand>(RetryPolicy retryPolicy) where TCommand : IJobCommand;

    IJobbyServicesConfigurable AddJob<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>;

    IJobbyServicesConfigurable AddJobsFromAssemblies(params Assembly[] assemblies);
}
