using Jobby.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jobby.Core.Interfaces.Configuration;

public interface IJobbyComponentsConfigurable
{
    IJobbyComponentsConfigurable UseStorage(IJobbyStorage storage);
    IJobbyComponentsConfigurable UseStorage(Func<ICommonInfrastructure, IJobbyStorage> createStorage);
    IJobbyComponentsConfigurable UseStorageMigrator(Func<ICommonInfrastructure, IJobbyStorageMigrator> createMigrator);

    IJobbyComponentsConfigurable UseLoggerFactory(ILoggerFactory loggerFactory);

    IJobbyComponentsConfigurable UseSerializer(IJobParamSerializer serializer);
    IJobbyComponentsConfigurable UseSystemTextJson(JsonSerializerOptions jsonOptions);

    IJobbyComponentsConfigurable UseServerSettings(JobbyServerSettings settings);

    IJobbyComponentsConfigurable UseExecutionScopeFactory(IJobExecutionScopeFactory scopeFactory);

    IJobbyComponentsConfigurable UseDefaultRetryPolicy(RetryPolicy retryPolicy);
    IJobbyComponentsConfigurable UseRetryPolicyForJob<TCommand>(RetryPolicy retryPolicy) where TCommand : IJobCommand;

    IJobbyComponentsConfigurable ConfigurePipeline(Action<IPipelineConfigurable> configure);

    IJobbyComponentsConfigurable UseMetrics();
    IJobbyComponentsConfigurable UseTracing();
}
