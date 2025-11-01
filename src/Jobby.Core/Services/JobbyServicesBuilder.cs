using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

namespace Jobby.Core.Services;

[Obsolete("Use JobbyBuilder instead of JobbyServicesBuilder. Will be removed in 1.0.0")]
public class JobbyServicesBuilder : IJobbyServicesConfigurable
{
    private readonly JobbyBuilder _jobbyBuilder = new JobbyBuilder();

    public bool IsExecutionScopeFactorySpecified => _jobbyBuilder.IsExecutionScopeFactorySpecified;
    public bool IsLoggerFactorySpecified => _jobbyBuilder.IsLoggerFactorySpecified;
    public IEnumerable<JobTypesMetadata> AddedJobTypes => _jobbyBuilder.AddedJobTypes;

    public IJobbyServer CreateJobbyServer()
    {
        return _jobbyBuilder.CreateJobbyServer();
    }

    public IJobbyClient CreateJobbyClient()
    {
        return _jobbyBuilder.CreateJobbyClient();
    }

    public IJobsFactory CreateJobsFactory()
    {
        return _jobbyBuilder.CreateJobsFactory();
    }

    public IJobbyServicesConfigurable UseExecutionScopeFactory(IJobExecutionScopeFactory scopeFactory)
    {
        _jobbyBuilder.UseExecutionScopeFactory(scopeFactory);
        return this;
    }

    public IJobbyServicesConfigurable UseLoggerFactory(ILoggerFactory loggerFactory)
    {
        _jobbyBuilder.UseLoggerFactory(loggerFactory);
        return this;
    }

    public IJobbyServicesConfigurable UseServerSettings(JobbyServerSettings serverSettings)
    {
        _jobbyBuilder.UseServerSettings(serverSettings);
        return this;
    }

    public IJobbyServicesConfigurable UseStorage(IJobbyStorage storage)
    {
        _jobbyBuilder.UseStorage(storage);
        return this;
    }

    public IJobbyServicesConfigurable UseSystemTextJson(JsonSerializerOptions jsonOptions)
    {
        UseSerializer(new SystemTextJsonJobParamSerializer(jsonOptions));
        return this;
    }

    public IJobbyServicesConfigurable UseSerializer(IJobParamSerializer serializer)
    {
        _jobbyBuilder.UseSerializer(serializer);
        return this;
    }

    public IJobbyServicesConfigurable UseDefaultRetryPolicy(RetryPolicy retryPolicy)
    {
        _jobbyBuilder.UseDefaultRetryPolicy(retryPolicy);
        return this;
    }

    public IJobbyServicesConfigurable UseRetryPolicyForJob<TCommand>(RetryPolicy retryPolicy) where TCommand : IJobCommand
    {
        _jobbyBuilder.UseRetryPolicyForJob<TCommand>(retryPolicy);
        return this;
    }

    public IJobbyServicesConfigurable AddJob<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>
    {
        _jobbyBuilder.AddJob<TCommand, THandler>();
        return this;
    }

    public IJobbyServicesConfigurable AddOrReplaceJob<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>
    {
        _jobbyBuilder.AddOrReplaceJob<TCommand, THandler>();
        return this;
    }

    public IJobbyServicesConfigurable AddJobsFromAssemblies(params Assembly[] assemblies)
    {
        _jobbyBuilder.AddJobsFromAssemblies(assemblies);
        return this;
    }
}
