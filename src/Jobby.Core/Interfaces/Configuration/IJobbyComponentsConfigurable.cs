using Jobby.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Interfaces.ServerModules.PermanentLockedGroupsCheck;

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
    
    IJobbyComponentsConfigurable UseScheduler<TScheduler, TSchedulerHandler>()
        where TScheduler : ISchedule
        where TSchedulerHandler : IScheduleHandler<TScheduler>;
    IJobbyComponentsConfigurable UseScheduler<TScheduler, TSchedulerHandler, TSchedulerSerializer>()
        where TScheduler : ISchedule
        where TSchedulerHandler : IScheduleHandler<TScheduler>
        where TSchedulerSerializer : IScheduleSerializer<TScheduler>;
}
