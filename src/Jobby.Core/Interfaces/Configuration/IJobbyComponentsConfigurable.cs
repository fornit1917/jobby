using System.Text.Json;

using Microsoft.Extensions.Logging;

using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Interfaces.ServerModules.PermanentLockedGroupsCheck;
using Jobby.Core.Models;

namespace Jobby.Core.Interfaces.Configuration;

public interface IJobbyComponentsConfigurable
{
    IJobbyComponentsConfigurable UseStorage(IJobbyStorage storage);
    IJobbyComponentsConfigurable UseStorage(Func<ICommonInfrastructure, IJobbyStorage> createStorage);
    IJobbyComponentsConfigurable UsePermanentLocksStorage(Func<ICommonInfrastructure, IPermanentLocksStorage> createPermanentLocksStorage);
    IJobbyComponentsConfigurable UseStorageMigrator(Func<ICommonInfrastructure, IJobbyStorageMigrator> createMigrator);

    IJobbyComponentsConfigurable UseLoggerFactory(ILoggerFactory loggerFactory);
    
    IJobbyComponentsConfigurable UseGuidGenerator(IGuidGenerator guidGenerator);

    IJobbyComponentsConfigurable UseSerializer(IJobParamSerializer serializer);
    IJobbyComponentsConfigurable UseSystemTextJson(JsonSerializerOptions jsonOptions);

    IJobbyComponentsConfigurable UseServerSettings(JobbyServerSettings settings);

    IJobbyComponentsConfigurable UseExecutionScopeFactory(IJobExecutionScopeFactory scopeFactory);

    IJobbyComponentsConfigurable UseDefaultRetryPolicy(RetryPolicy retryPolicy);
    IJobbyComponentsConfigurable UseRetryPolicyForJob<TCommand>(RetryPolicy retryPolicy) where TCommand : IJobCommand;

    IJobbyComponentsConfigurable ConfigurePipeline(Action<IPipelineConfigurable> configure);

    IJobbyComponentsConfigurable UseMetrics();
    IJobbyComponentsConfigurable UseTracing();

    IJobbyComponentsConfigurable UseScheduler<TScheduler>(ISchedulerStorage<TScheduler> schedulerStorage, string? prefix = null)
        where TScheduler : IScheduler;
}
