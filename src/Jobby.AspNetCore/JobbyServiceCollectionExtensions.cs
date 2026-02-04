using Jobby.Core.Interfaces;
using Jobby.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobby.AspNetCore;

public static class JobbyServiceCollectionExtensions
{
    public static IServiceCollection AddJobbyServerAndClient(this IServiceCollection services, Action<IAspNetCoreJobbyConfigurable> configure)
    {
        var wrappedBuilder = new AspNetCoreJobbyBuilder();
        configure.Invoke(wrappedBuilder);

        // register handlers for added jobs
        foreach (var jobTypesMetadata in wrappedBuilder.JobbyBuilder.AddedJobTypes)
        {
            services.AddScoped(jobTypesMetadata.HandlerType, jobTypesMetadata.HandlerImplType);
        }

        services.AddSingleton<JobbyBuilder>(sp =>
        {
            wrappedBuilder.ApplyJobbyComponentsConfigureAction(sp);

            var jobbyBuilder = wrappedBuilder.JobbyBuilder;
            if (!jobbyBuilder.IsExecutionScopeFactorySpecified)
            {
                var aspNetScopeExecutionFactory = new AspNetCoreJobExecutionScopeFactory(sp);
                jobbyBuilder.UseExecutionScopeFactory(aspNetScopeExecutionFactory);
            }

            if (!jobbyBuilder.IsLoggerFactorySpecified)
            {
                var loggerFactoryFromDi = sp.GetService<ILoggerFactory>();
                if (loggerFactoryFromDi != null)
                {
                    jobbyBuilder.UseLoggerFactory(loggerFactoryFromDi);
                }
            }
            
            return jobbyBuilder;
        });

        services.AddSingleton<IJobbyStorageMigrator>(sp =>
            sp.GetRequiredService<JobbyBuilder>().CreateStorageMigrator());
        services.AddSingleton<IJobsFactory>(sp => sp.GetRequiredService<JobbyBuilder>().CreateJobsFactory());
        services.AddSingleton<IJobbyClient>(sp => sp.GetRequiredService<JobbyBuilder>().CreateJobbyClient());
        services.AddSingleton<IJobbyServer>(sp => sp.GetRequiredService<JobbyBuilder>().CreateJobbyServer());
        services.AddHostedService<JobbyHostedService>();
        return services;
    }

    public static IServiceCollection AddJobbyClient(this IServiceCollection services, Action<IAspNetCoreJobbyConfigurable> configure)
    {
        var wrappedBuilder = new AspNetCoreJobbyBuilder();
        
        configure.Invoke(wrappedBuilder);
        
        services.AddSingleton<JobbyBuilder>(sp =>
        {
            wrappedBuilder.ApplyJobbyComponentsConfigureAction(sp);
            return wrappedBuilder.JobbyBuilder;
        });

        services.AddSingleton<IJobsFactory>(sp => sp.GetRequiredService<JobbyBuilder>().CreateJobsFactory());
        services.AddSingleton<IJobbyClient>(sp => sp.GetRequiredService<JobbyBuilder>().CreateJobbyClient());
        services.AddSingleton<IJobbyStorageMigrator>(sp => sp.GetRequiredService<JobbyBuilder>().CreateStorageMigrator());
        return services;
    }
}
