using Jobby.Core.Exceptions;
using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Configuration;
using Jobby.Core.Models;
using Jobby.Core.Services.HandlerPipeline;
using Jobby.Core.Services.Observability;
using Microsoft.Extensions.Logging;
using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;

namespace Jobby.Core.Services;

public class JobbyBuilder : IJobbyComponentsConfigurable, IJobbyJobsConfigurable
{
    private IJobbyStorage? _storage;
    private IJobExecutionScopeFactory? _scopeFactory;
    private ILoggerFactory? _loggerFactory;
    private IJobParamSerializer? _serializer;
    private IJobsFactory? _jobsFactory;

    private MetricsMiddleware? _metricsMiddleware;
    private readonly PipelineBuilder _pipelineBuilder = new PipelineBuilder();

    private RetryPolicy _defaultRetryPolicy = RetryPolicy.NoRetry;
    private Dictionary<string, RetryPolicy> _retryPolicyByJobName = new Dictionary<string, RetryPolicy>();
    private IRetryPolicyService? _retryPolicyService;

    private readonly Dictionary<string, IJobExecutor> _jobExecutorsByJobName = new();
    private IJobsRegistry? _jobsRegistry;

    public bool IsExecutionScopeFactorySpecified => _scopeFactory != null;
    public bool IsLoggerFactorySpecified => _loggerFactory != null;
    public IEnumerable<JobTypesMetadata> AddedJobTypes => _jobExecutorsByJobName.Values.Select(x => x.GetJobTypesMetadata());

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
            _retryPolicyService = new RetryPolicyService(_defaultRetryPolicy, _retryPolicyByJobName);
        }

        if (_jobsRegistry == null)
        {
            _jobsRegistry = new JobsRegistry(_jobExecutorsByJobName.ToFrozenDictionary());
        }

        var serverId = $"{Environment.MachineName}_{Guid.NewGuid()}";

        IJobCompletionService completionService = _serverSettings.CompleteWithBatching
            ? new BatchingJobCompletionService(_storage, _serverSettings, serverId)
            : new SimpleJobCompletionService(_storage, _serverSettings.DeleteCompleted, serverId);

        IJobPostProcessingService postProcessingService = new JobPostProcessingService(_storage,
            completionService,
            _loggerFactory.CreateLogger<JobPostProcessingService>(),
            serverId);

        IJobExecutionService executionService = new JobExecutionService(_scopeFactory,
            _jobsRegistry,
            _retryPolicyService,
            _serializer,
            _pipelineBuilder,
            postProcessingService,
            _loggerFactory.CreateLogger<JobExecutionService>());

        return new JobbyServer(_storage,
            executionService,
            postProcessingService,
            _loggerFactory.CreateLogger<JobbyServer>(),
            _serverSettings,
            serverId);
    }

    public IJobbyClient CreateJobbyClient()
    {
        if (_storage == null)
        {
            throw new InvalidBuilderConfigException("Jobs storage is not specified");
        }

        return new JobbyClient(CreateJobsFactory(), _storage);
    }

    public IJobsFactory CreateJobsFactory()
    {
        if (_jobsFactory == null)
        {
            if (_serializer == null)
            {
                _serializer = new SystemTextJsonJobParamSerializer(new JsonSerializerOptions());
            }
            _jobsFactory = new JobsFactory(_serializer);
        }
        return _jobsFactory;
    }

    public IJobbyComponentsConfigurable UseExecutionScopeFactory(IJobExecutionScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        return this;
    }

    public IJobbyComponentsConfigurable UseLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        return this;
    }

    public IJobbyComponentsConfigurable UseServerSettings(JobbyServerSettings serverSettings)
    {
        _serverSettings = serverSettings;
        return this;
    }

    public IJobbyComponentsConfigurable UseStorage(IJobbyStorage storage)
    {
        _storage = storage;
        return this;
    }

    public IJobbyComponentsConfigurable UseSystemTextJson(JsonSerializerOptions jsonOptions)
    {
        UseSerializer(new SystemTextJsonJobParamSerializer(jsonOptions));
        return this;
    }

    public IJobbyComponentsConfigurable UseSerializer(IJobParamSerializer serializer)
    {
        _serializer = serializer;
        _jobsFactory = new JobsFactory(_serializer);
        return this;
    }

    public IJobbyComponentsConfigurable UseDefaultRetryPolicy(RetryPolicy retryPolicy)
    {
        _defaultRetryPolicy = retryPolicy;
        return this;
    }

    public IJobbyComponentsConfigurable UseRetryPolicyForJob<TCommand>(RetryPolicy retryPolicy) where TCommand : IJobCommand
    {
        _retryPolicyByJobName[TCommand.GetJobName()] = retryPolicy;
        return this;
    }

    public IJobbyJobsConfigurable AddJob<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>
    {
        var jobName = TCommand.GetJobName();
        if (!_jobExecutorsByJobName.TryAdd(jobName, new JobExecutor<TCommand, THandler>()))
        {
            throw new InvalidJobsConfigException($"Handler for {typeof(TCommand)} has already been added");
        }
        return this;
    }

    public IJobbyJobsConfigurable AddOrReplaceJob<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>
    {
        var jobName = TCommand.GetJobName();
        _jobExecutorsByJobName[jobName] = new JobExecutor<TCommand, THandler>();
        return this;
    }

    public IJobbyJobsConfigurable AddJobsFromAssemblies(params Assembly[] assemblies)
    {
        var commandTypesByJobName = new Dictionary<string, Type>();
        var handlerImplTypesByCommandType = new Dictionary<Type, Type>();

        var notAbstractTypes = assemblies.SelectMany(a => a.GetTypes()).Where(a => !a.IsAbstract);

        foreach (var t in notAbstractTypes)
        {
            var jobName = ReflectionHelper.TryGetJobNameByType(t);
            if (jobName != null)
            {
                if (!commandTypesByJobName.TryAdd(jobName, t))
                {
                    var error = $"Each implementation of IJobCommand must return unique value from GetJobName method, but name '{jobName}' is used in two commands: {commandTypesByJobName} and {t}";
                    throw new InvalidJobsConfigException(error);
                }
            }

            var commandFromHandler = ReflectionHelper.TryGetCommandTypeFromHandlerType(t);
            if (commandFromHandler != null)
            {
                if (!handlerImplTypesByCommandType.TryAdd(commandFromHandler, t))
                {
                    var error = $"Each job command must have single handler, but command {commandFromHandler} has two handlers: {handlerImplTypesByCommandType[t]} and {t}";
                    throw new InvalidJobsConfigException(error);
                }
            }
        }

        foreach (var keyVal in commandTypesByJobName)
        {
            var jobName = keyVal.Key;
            var commandType = keyVal.Value;

            if (!handlerImplTypesByCommandType.TryGetValue(commandType, out var handlerImplType))
            {
                var error = $"Command {commandType} does not have handler. IJobCommandHandler<> should be implemented for this type";
                throw new InvalidJobsConfigException(error);
            }

            var jobExecutorType = typeof(JobExecutor<,>).MakeGenericType(commandType, handlerImplType);
            var jobExecutor = Activator.CreateInstance(jobExecutorType) as IJobExecutor;

            if (jobExecutor is null)
            {
                throw new InvalidJobsConfigException($"Could not create instance of JobExecutorFactory with type {jobExecutorType}");
            }

            _jobExecutorsByJobName.Add(jobName, jobExecutor);
        }

        return this;
    }

    public IJobbyComponentsConfigurable ConfigurePipeline(Action<IPipelineConfigurable> configure)
    {
        configure(_pipelineBuilder);
        return this;
    }

    public IJobbyComponentsConfigurable UseMetrics()
    {
        if (_metricsMiddleware == null)
        {
            _metricsMiddleware = new MetricsMiddleware(MetricsService.Instance);
            _pipelineBuilder.UseAsOuter(_metricsMiddleware);
        }
        return this;
    }
}
