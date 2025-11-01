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
            return wrappedBuilder.JobbyBuilder;
        });

        services.AddSingleton<IJobsFactory>(sp => sp.GetRequiredService<JobbyBuilder>().CreateJobsFactory());
        services.AddSingleton<IJobbyClient>(sp => sp.GetRequiredService<JobbyBuilder>().CreateJobbyClient());
        services.AddSingleton<IJobbyServer>(sp =>
        {
            var jobbyBuilder = sp.GetRequiredService<JobbyBuilder>();
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

            return jobbyBuilder.CreateJobbyServer();
        });
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
        return services;
    }

    [Obsolete("Use AddJobbyServerAndClient(Action<IAspNetCoreJobbyConfigurable>)")]
    public static IServiceCollection AddJobby(this IServiceCollection services, Action<IJobbyServicesConfigurable> configure)
    {
        var jobbyServicesBuilder = new JobbyServicesBuilder();

        configure.Invoke(jobbyServicesBuilder);

        // register handlers for added jobs
        foreach (var jobTypesMetadata in jobbyServicesBuilder.AddedJobTypes)
        {
            services.AddScoped(jobTypesMetadata.HandlerType, jobTypesMetadata.HandlerImplType);
        }

        services.AddSingleton<IJobsFactory>(_ => jobbyServicesBuilder.CreateJobsFactory());
        services.AddSingleton<IJobbyClient>(_ => jobbyServicesBuilder.CreateJobbyClient());
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

    [Obsolete("Use AddJobbyClient(Action<IAspNetCoreJobbyConfigurable>)")]
    public static IServiceCollection AddJobbyClient(this IServiceCollection services, Action<IJobbyServicesConfigurable> configure)
    {
        var jobbyServicesBuilder = new JobbyServicesBuilder();
        configure.Invoke(jobbyServicesBuilder);
        services.AddSingleton<IJobbyClient>(_ => jobbyServicesBuilder.CreateJobbyClient());
        services.AddSingleton<IJobsFactory>(_ => jobbyServicesBuilder.CreateJobsFactory());
        return services;
    }
}
