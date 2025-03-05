using Jobby.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jobby.Core.Interfaces.Builders;

public interface IJobbyServicesConfigurable
{
    IJobbyServicesConfigurable UseStorage(IJobsStorage storage);
    IJobbyServicesConfigurable UseLoggerFactory(ILoggerFactory loggerFactory);
    IJobbyServicesConfigurable UseSystemTextJson(JsonSerializerOptions jsonOptions);
    IJobbyServicesConfigurable UseServerSettings(JobbyServerSettings settings);
    IJobbyServicesConfigurable UseExecutionScopeFactory(IJobExecutionScopeFactory scopeFactory);
    IJobbyServicesConfigurable UseRetryPolicy(Action<IRetryPolicyConfigurable> configureRetryPolicy);
    IJobbyServicesConfigurable UseJobs(Action<IJobsRegistryConfigurable> configureJobsRegistry);
}
