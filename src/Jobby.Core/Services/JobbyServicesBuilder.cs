using Jobby.Core.Exceptions;
using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Frozen;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text.Json;

namespace Jobby.Core.Services;

public class JobbyServicesBuilder : IJobbyServicesConfigurable
{
    private IJobbyStorage? _storage;
    private IJobExecutionScopeFactory? _scopeFactory;
    private ILoggerFactory? _loggerFactory;
    private IJobParamSerializer? _serializer;

    private RetryPolicy _defaultRetryPolicy = RetryPolicy.NoRetry;
    private Dictionary<string, RetryPolicy> _retryPolicyByJobName = new Dictionary<string, RetryPolicy>();
    private IRetryPolicyService? _retryPolicyService;

    private readonly Dictionary<string, JobExecutionMetadata> _jobExecMetadataByJobName = new();
    private IJobsRegistry? _jobsRegistry;

    public bool IsExecutionScopeFactorySpecified => _scopeFactory != null;
    public bool IsLoggerFactorySpecified => _loggerFactory != null;
    public IEnumerable<IJobTypesMetadata> AddedJobTypes => _jobExecMetadataByJobName.Values;

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
            _jobsRegistry = new JobsRegistry(_jobExecMetadataByJobName.ToFrozenDictionary());
        }

        IJobCompletionService completionService = _serverSettings.CompleteWithBatching
            ? new BatchingJobCompletionService(_storage, _serverSettings)
            : new SimpleJobCompletionService(_storage, _serverSettings.DeleteCompleted);

        IJobPostProcessingService postProcessingService = new JobPostProcessingService(_storage,
            completionService,
            _loggerFactory.CreateLogger<JobPostProcessingService>(),
            _serverSettings);

        IJobExecutionService executionService = new JobExecutionService(_scopeFactory,
            _jobsRegistry,
            _retryPolicyService,
            _serializer,
            postProcessingService,
            _loggerFactory.CreateLogger<JobExecutionService>());

        return new JobbyServer(_storage,
            executionService,
            postProcessingService,
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

    public IJobbyServicesConfigurable UseExecutionScopeFactory(IJobExecutionScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        return this;
    }

    public IJobbyServicesConfigurable UseLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
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

    public IJobbyServicesConfigurable UseDefaultRetryPolicy(RetryPolicy retryPolicy)
    {
        _defaultRetryPolicy = retryPolicy;
        return this;
    }

    public IJobbyServicesConfigurable UseRetryPolicyForJob<TCommand>(RetryPolicy retryPolicy) where TCommand : IJobCommand
    {
        _retryPolicyByJobName[TCommand.GetJobName()] = retryPolicy;
        return this;
    }

    public IJobbyServicesConfigurable AddJob<TCommand, THandler>()
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>
    {
        var jobName = TCommand.GetJobName();
        var handlerType = typeof(IJobCommandHandler<TCommand>);
        var execMethod = handlerType.GetMethod("ExecuteAsync", [typeof(TCommand), typeof(JobExecutionContext)]);
        if (execMethod == null)
        {
            throw new ArgumentException($"Type {handlerType} does not have suitable ExecuteAsync method");
        }

        var execMetadata = new JobExecutionMetadata
        {
            CommandType = typeof(TCommand),
            HandlerType = handlerType,
            HandlerImplType = typeof(THandler),
            ExecMethod = execMethod
        };

        _jobExecMetadataByJobName[jobName] = execMetadata;

        return this;
    }

    public IJobbyServicesConfigurable AddJobsFromAssemblies(params Assembly[] assemblies)
    {
        var commandTypesByJobName = new Dictionary<string, Type>();
        var handlerImplTypesByCommandType = new Dictionary<Type, Type>();

        var notAbstractTypes = assemblies.SelectMany(a => a.GetTypes()).Where(a => !a.IsAbstract);

        foreach (var t in notAbstractTypes)
        {
            var jobName = ReflectionHelper.TryGetJobNameByType(t);
            if (jobName != null)
            {
                if (commandTypesByJobName.ContainsKey(jobName))
                {
                    var error = $"Each implementation of IJobCommand must return unique value from GetJobName method, but name '{jobName}' is used in two commands: {commandTypesByJobName} and {t}";
                    throw new InvalidJobsConfigException(error);
                }

                commandTypesByJobName[jobName] = t;
            }

            var commandFromHandler = ReflectionHelper.TryGetCommandTypeFromHandlerType(t);
            if (commandFromHandler != null)
            {
                if (handlerImplTypesByCommandType.ContainsKey(commandFromHandler))
                {
                    var error = $"Each job command must have single handler, but command {commandFromHandler} has two handlers: {handlerImplTypesByCommandType[t]} and {t}";
                    throw new InvalidJobsConfigException(error);
                }

                handlerImplTypesByCommandType[commandFromHandler] = t;
            }
        }

        foreach (var keyVal in commandTypesByJobName)
        {
            var jobName = keyVal.Key;
            var commandType = keyVal.Value;
            if (!handlerImplTypesByCommandType.ContainsKey(commandType))
            {
                var error = $"Command {commandType} does not have handler. IJobCommandHandler<> should be implemented for this type";
                throw new InvalidJobsConfigException(error);
            }

            var handlerType = typeof(IJobCommandHandler<>).MakeGenericType(commandType);
            var handlerImplType = handlerImplTypesByCommandType[commandType];
            var execMethod = handlerType.GetMethod("ExecuteAsync", [commandType, typeof(JobExecutionContext)]);
            if (execMethod == null)
            {
                throw new ArgumentException($"Type {handlerType} does not have suitable ExecuteAsync method");
            }

            var execMetadata = new JobExecutionMetadata
            {
                CommandType = commandType,
                HandlerType = handlerType,
                HandlerImplType = handlerImplType,
                ExecMethod = execMethod
            };

            _jobExecMetadataByJobName[jobName] = execMetadata;
        }

        return this;
    }
}
