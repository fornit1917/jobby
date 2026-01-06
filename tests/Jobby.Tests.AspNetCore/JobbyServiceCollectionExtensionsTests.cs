using Jobby.AspNetCore;
using Jobby.Core.Interfaces;
using Jobby.Core.Services;
using Jobby.TestsUtils.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Jobby.Tests.AspNetCore;

public class JobbyServiceCollectionExtensionsTests
{
    private readonly Mock<IServiceCollection> _serviceCollectionMock;
    private readonly List<ServiceDescriptor> _addedServices;

    public JobbyServiceCollectionExtensionsTests()
    {
        _addedServices = new List<ServiceDescriptor>();
        _serviceCollectionMock = new Mock<IServiceCollection>();
        _serviceCollectionMock
            .Setup(x => x.Add(It.IsAny<ServiceDescriptor>()))
            .Callback<ServiceDescriptor>(x => _addedServices.Add(x));
    }

    [Fact]
    public void AddJobbyClient_WithoutServiceProvider_AddsJobbyClientAndFactoryAsSingletons()
    {
        _serviceCollectionMock.Object.AddJobbyClient((IAspNetCoreJobbyConfigurable x) =>
        {
            x.ConfigureJobby(jobby => jobby.UseStorage(new Mock<IJobbyStorage>().Object));
        });

        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobsFactory) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobbyClient) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobbyStorageMigrator) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(JobbyBuilder) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Equal(4, _addedServices.Count);
    }

    [Fact]
    public void AddJobbyClient_WithServiceProvider_AddsJobbyClientAndFactoryAsSingletons()
    {
        _serviceCollectionMock.Object.AddJobbyClient((IAspNetCoreJobbyConfigurable x) =>
        {
            x.ConfigureJobby((sp, jobby) => jobby.UseStorage(new Mock<IJobbyStorage>().Object));
        });

        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobsFactory) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobbyClient) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobbyStorageMigrator) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(JobbyBuilder) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Equal(4, _addedServices.Count);
    }

    [Fact]
    public void AddJobbyServerAndClient_WithoutServiceProvider_AddsJobHandlersAndJobbyServices()
    {
        _serviceCollectionMock.Object.AddJobbyServerAndClient(x =>
        {
            x.AddJob<TestJobCommand, TestJobCommandHandler>();
            x.ConfigureJobby(jobby =>
            {
                jobby.UseStorage(new Mock<IJobbyStorage>().Object);
                jobby.UseExecutionScopeFactory(new Mock<IJobExecutionScopeFactory>().Object);
            });
        });

        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobCommandHandler<TestJobCommand>)
                                             && x.ImplementationType == typeof(TestJobCommandHandler)
                                             && x.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobbyStorageMigrator) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobsFactory) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobbyClient) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobbyServer) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(JobbyBuilder) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IHostedService)
                                             && x.ImplementationType == typeof(JobbyHostedService));
    }

    [Fact]
    public void AddJobbyServerAndClient_WithServiceProvider_AddsJobHandlersAndJobbyServices()
    {
        _serviceCollectionMock.Object.AddJobbyServerAndClient(x =>
        {
            x.AddJob<TestJobCommand, TestJobCommandHandler>();
            x.ConfigureJobby((sp, jobby) =>
            {
                jobby.UseStorage(new Mock<IJobbyStorage>().Object);
                jobby.UseExecutionScopeFactory(new Mock<IJobExecutionScopeFactory>().Object);
            });
        });

        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobCommandHandler<TestJobCommand>)
                                             && x.ImplementationType == typeof(TestJobCommandHandler)
                                             && x.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobbyStorageMigrator) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobsFactory) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobbyClient) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobbyServer) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(JobbyBuilder) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IHostedService)
                                             && x.ImplementationType == typeof(JobbyHostedService));
    }

    [Fact]
    [Obsolete]
    public void AddJobbyClientDeprecated_AddsJobbyClientAndFactoryAsSingletons()
    {
        _serviceCollectionMock.Object.AddJobbyClient(x =>
        {
            x.UseStorage(new Mock<IJobbyStorage>().Object);
        });

        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobsFactory) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobbyClient) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Equal(2, _addedServices.Count);
    }

    [Fact]
    [Obsolete]
    public void AddJobbyDeprecated_AddsJobHandlersAndJobbyServices()
    {
        _serviceCollectionMock.Object.AddJobby(x =>
        {
            x.UseStorage(new Mock<IJobbyStorage>().Object);
            x.UseExecutionScopeFactory(new Mock<IJobExecutionScopeFactory>().Object);
            x.AddJob<TestJobCommand, TestJobCommandHandler>();
        });

        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobCommandHandler<TestJobCommand>)
                                             && x.ImplementationType == typeof(TestJobCommandHandler)
                                             && x.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobsFactory) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobbyClient) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IJobbyServer) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(_addedServices, x => x.ServiceType == typeof(IHostedService) 
                                             && x.ImplementationType == typeof(JobbyHostedService));
    }
}
