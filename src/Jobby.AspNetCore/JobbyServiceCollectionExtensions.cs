using Jobby.Core.Interfaces;
using Jobby.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobby.AspNetCore;

public static class JobbyServiceCollectionExtensions
{
    public static IServiceCollection AddJobby(this IServiceCollection services, Action<IJobbyServicesConfigurable> configure = null)
    {
        var jobbyServicesBuilder = new JobbyServicesBuilder();
        
        configure?.Invoke(jobbyServicesBuilder);

        foreach (var jobTypesMetadata in jobbyServicesBuilder.AddedJobTypes)
        {
            services.AddScoped(jobTypesMetadata.HandlerType, jobTypesMetadata.HandlerImplType);
        }

        services.AddSingleton<IJobsClient>(_ => jobbyServicesBuilder.CreateJobsClient());
        services.AddSingleton<IJobbyServer>(sp =>
        {
            if (!jobbyServicesBuilder.IsExecutionScopeFactorySpecified)
            {
                var aspNetScopeExecutionFactory = new AspNetCoreJobExecutionScopeFactory(sp);
                jobbyServicesBuilder.UseExecutionScopeFactory(aspNetScopeExecutionFactory);
            }

            if (!jobbyServicesBuilder.IsLoggerFactorySpecified)
            {
                var loggerFactoryFromDi = sp.GetService<ILoggerFactory>();
                if (loggerFactoryFromDi != null)
                {
                    jobbyServicesBuilder.UseLoggerFactory(loggerFactoryFromDi);
                }
            }

            return jobbyServicesBuilder.CreateJobbyServer();
        });
        services.AddHostedService<JobbyHostedService>();
        return services;
    }
}
