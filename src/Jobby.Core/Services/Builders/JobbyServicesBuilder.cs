using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Builders;
using Jobby.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jobby.Core.Services.Builders;

public class JobbyServicesBuilder : IJobbyServicesConfigurable, IJobbyServicesBuilder
{
    private IJobbyStorage? _storage;
    private IJobExecutionScopeFactory? _scopeFactory;
    private ILoggerFactory? _loggerFactory;
    private IJobParamSerializer? _serializer;
    private IRetryPolicyService? _retryPolicyService;
    private IJobsRegistry? _jobsRegistry;
    private JobbyServerSettings _serverSettings = new JobbyServerSettings();

    public IJobbyServer CreateJobbyServer()
    {
        if (_storage == null)
        {
            throw new InvalidBuilderConfigException("Storage is not specified");
        }
        if (_scopeFactory == null) 
        {
            throw new InvalidBuilderConfigException("ExecutionScopeFactory is not specified");
        }
        if (_serializer == null)
        {
            _serializer = new SystemTextJsonJobParamSerializer(new JsonSerializerOptions());
        }
        if (_loggerFactory == null)
        {
            _loggerFactory = new EmptyLoggerFactory();
        }
        if (_retryPolicyService == null)
        {
            var builder = new RetryPolicyBuilder();
            _retryPolicyService = builder.Build();
        }
        if (_jobsRegistry == null)
        {
            throw new InvalidBuilderConfigException("Jobs is not configured. UseJobs should be called");
        }

        return new JobbyServer(_storage,
            _scopeFactory,
            _retryPolicyService, 
            _jobsRegistry, 
            _serializer,
            _loggerFactory.CreateLogger<JobbyServer>(),
            _serverSettings);
    }

    public IJobsClient CreateJobsClient()
    {
        if (_storage == null)
        {
            throw new InvalidBuilderConfigException("Jobs storage is not specified");
        }
        if (_serializer == null)
        {
            _serializer = new SystemTextJsonJobParamSerializer(new JsonSerializerOptions());
        }
        var jobsFactory = new JobsFactory(_serializer);
        return new JobsClient(jobsFactory, _storage);
    }

    public IRecurrentJobsClient CreateRecurrentJobsClient()
    {
        if (_storage == null)
        {
            throw new InvalidBuilderConfigException("Jobs storage is not specified");
        }
        return new RecurrentJobsClient(_storage);
    }

    public IJobbyServicesConfigurable UseExecutionScopeFactory(IJobExecutionScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        return this;
    }

    public IJobbyServicesConfigurable UseJobs(Action<IJobsRegistryConfigurable> configureJobsRegistry)
    {
        var builder = new JobsRegistryBuilder();
        configureJobsRegistry(builder);
        _jobsRegistry = builder.Build();
        return this;
    }

    public IJobbyServicesConfigurable UseLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        return this;
    }

    public IJobbyServicesConfigurable UseRetryPolicy(Action<IRetryPolicyConfigurable> configureRetryPolicy)
    {
        var builder = new RetryPolicyBuilder();
        configureRetryPolicy(builder);
        _retryPolicyService = builder.Build();
        return this;
    }

    public IJobbyServicesConfigurable UseServerSettings(JobbyServerSettings serverSettings)
    {
        _serverSettings = serverSettings;
        return this;
    }

    public IJobbyServicesConfigurable UseStorage(IJobbyStorage storage)
    {
        _storage = storage;
        return this;
    }

    public IJobbyServicesConfigurable UseSystemTextJson(JsonSerializerOptions jsonOptions)
    {
        _serializer = new SystemTextJsonJobParamSerializer(jsonOptions);
        return this;
    }
}
