using Jobby.Core.Models;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

namespace Jobby.Core.Interfaces;

[Obsolete("Use IJobbyComponentsConfigurable and IJobbyJobsConfigurable instead of IJobbyServicesConfigurable. It will be removed in 1.0.0")]
public interface IJobbyServicesConfigurable
{
    IJobbyServicesConfigurable UseStorage(IJobbyStorage storage);

    IJobbyServicesConfigurable UseLoggerFactory(ILoggerFactory loggerFactory);

    IJobbyServicesConfigurable UseSerializer(IJobParamSerializer serializer);
    IJobbyServicesConfigurable UseSystemTextJson(JsonSerializerOptions jsonOptions);

    IJobbyServicesConfigurable UseServerSettings(JobbyServerSettings settings);

    IJobbyServicesConfigurable UseExecutionScopeFactory(IJobExecutionScopeFactory scopeFactory);

    IJobbyServicesConfigurable UseDefaultRetryPolicy(RetryPolicy retryPolicy);
    IJobbyServicesConfigurable UseRetryPolicyForJob<TCommand>(RetryPolicy retryPolicy) where TCommand : IJobCommand;

    IJobbyServicesConfigurable AddJob<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>;

    IJobbyServicesConfigurable AddOrReplaceJob<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>;

    IJobbyServicesConfigurable AddJobsFromAssemblies(params Assembly[] assemblies);
}
