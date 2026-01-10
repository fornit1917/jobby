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

public class JobbyBuilder : IJobbyComponentsConfigurable, IJobbyJobsConfigurable, ICommonInfrastructure
{
    private static readonly ILoggerFactory DefaultLoggerFactory = new EmptyLoggerFactory();
    private static readonly IJobParamSerializer DefaultSerializer = new SystemTextJsonJobParamSerializer(new JsonSerializerOptions());
    private static readonly IJobbyStorageMigrator DefaultStorageMigrator = new EmptyStorageMigrator();
    private static readonly IGuidGenerator DefaultGuidGenerator = new GuidV7Generator();
    
    private IJobbyStorage? _storage;
    private Func<ICommonInfrastructure, IJobbyStorage>? _storageFactory;
    
    private IJobbyStorageMigrator? _storageMigrator;
    private Func<ICommonInfrastructure, IJobbyStorageMigrator>? _storageMigratorFactory;
    
    private IJobExecutionScopeFactory? _scopeFactory;
    private ILoggerFactory? _loggerFactory;
    private IJobParamSerializer? _serializer;

    private IGuidGenerator? _guidGenerator;
    
    private IJobsFactory? _jobsFactory;

    private MetricsMiddleware? _metricsMiddleware;
    private TracingMiddleware? _tracingMiddleware;
    private readonly PipelineBuilder _pipelineBuilder = new();

    private RetryPolicy _defaultRetryPolicy = RetryPolicy.NoRetry;
    private readonly Dictionary<string, RetryPolicy> _retryPolicyByJobName = new();
    private IRetryPolicyService? _retryPolicyService;

    private readonly Dictionary<string, IJobExecutor> _jobExecutorsByJobName = new();
    private IJobsRegistry? _jobsRegistry;

    private JobbyServerSettings _serverSettings = new();
    
    public bool IsExecutionScopeFactorySpecified => _scopeFactory != null;
    public bool IsLoggerFactorySpecified => _loggerFactory != null;
    public IEnumerable<JobTypesMetadata> AddedJobTypes => _jobExecutorsByJobName
        .Values
        .Select(x => x.GetJobTypesMetadata());
    
    public ILoggerFactory LoggerFactory => _loggerFactory ?? DefaultLoggerFactory;
    public IJobParamSerializer Serializer => _serializer ?? DefaultSerializer;
    public IGuidGenerator GuidGenerator => _guidGenerator ?? DefaultGuidGenerator;
    public JobbyServerSettings ServerSettings => _serverSettings;

    public IJobbyServer CreateJobbyServer()
    {
        if (_scopeFactory == null)
        {
            throw new InvalidBuilderConfigException("ExecutionScopeFactory is not specified");
        }
        
        _retryPolicyService ??= new RetryPolicyService(_defaultRetryPolicy, _retryPolicyByJobName);
        _jobsRegistry ??= new JobsRegistry(_jobExecutorsByJobName.ToFrozenDictionary());

        if (_metricsMiddleware != null)
        {
            _pipelineBuilder.UseAsOuter(_metricsMiddleware);
        }
        if (_tracingMiddleware != null)
        {
            _pipelineBuilder.UseAsOuter(_tracingMiddleware);
        }

        var storage = GetStorage();
        var serverId = $"{Environment.MachineName}_{Guid.NewGuid()}";

        IJobCompletionService completionService = _serverSettings.CompleteWithBatching
            ? new BatchingJobCompletionService(storage, _serverSettings, serverId)
            : new SimpleJobCompletionService(storage, _serverSettings.DeleteCompleted, serverId);

        IJobPostProcessingService postProcessingService = new JobPostProcessingService(storage,
            completionService,
            LoggerFactory.CreateLogger<JobPostProcessingService>(),
            serverId);

        IJobExecutionService executionService = new JobExecutionService(_scopeFactory,
            _jobsRegistry,
            _retryPolicyService,
            Serializer,
            _pipelineBuilder,
            postProcessingService,
            LoggerFactory.CreateLogger<JobExecutionService>());

        return new JobbyServer(storage,
            executionService,
            postProcessingService,
            LoggerFactory.CreateLogger<JobbyServer>(),
            _serverSettings,
            serverId);
    }

    public IJobbyClient CreateJobbyClient()
    {
        return new JobbyClient(CreateJobsFactory(), GetStorage());
    }

    public IJobsFactory CreateJobsFactory()
    {
        _jobsFactory ??= new JobsFactory(GuidGenerator, Serializer);
        return _jobsFactory;
    }

    public IJobbyStorageMigrator CreateStorageMigrator()
    {
        if (_storageMigrator == null && _storageMigratorFactory != null)
        {
            _storageMigrator = _storageMigratorFactory.Invoke(this);
        }
        return _storageMigrator ?? DefaultStorageMigrator;
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

    public IJobbyComponentsConfigurable UseGuidGenerator(IGuidGenerator guidGenerator)
    {
        _guidGenerator = guidGenerator;
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
    
    public IJobbyComponentsConfigurable UseStorage(Func<ICommonInfrastructure, IJobbyStorage> createStorage)
    {
        _storageFactory = createStorage;
        return this;
    }
    
    public IJobbyComponentsConfigurable UseStorageMigrator(Func<ICommonInfrastructure, IJobbyStorageMigrator> createMigrator)
    {
        _storageMigratorFactory = createMigrator;
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
        _metricsMiddleware ??= new MetricsMiddleware(MetricsService.Instance);
        return this;
    }

    public IJobbyComponentsConfigurable UseTracing()
    {
        _tracingMiddleware ??= new TracingMiddleware();
        return this;
    }

    private IJobbyStorage GetStorage()
    {
        if (_storage == null && _storageFactory != null)
        {
            _storage = _storageFactory.Invoke(this);
        }
        
        return _storage ?? throw new InvalidBuilderConfigException("Storage is not specified");
    }
}
