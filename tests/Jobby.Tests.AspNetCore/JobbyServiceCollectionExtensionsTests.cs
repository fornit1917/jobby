using Jobby.AspNetCore;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.ServerModules.PermanentLockedGroupsCheck;
using Jobby.Core.Services;
using Jobby.TestsUtils.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    public void AddJobbyServerAndClient_FullGraph_ResolvesAllComponents()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJobbyServerAndClient(cfg =>
        {
            cfg.AddJob<TestJobCommand, TestJobCommandHandler>();
            cfg.ConfigureJobby(jobby =>
            {
                jobby.UseStorage(new Mock<IJobbyStorage>().Object);
                jobby.UsePermanentLocksStorage(_ => new Mock<IPermanentLocksStorage>().Object);
            });
        });
        using var provider = services.BuildServiceProvider();

        // core components resolve
        var server = provider.GetRequiredService<IJobbyServer>();
        var client = provider.GetRequiredService<IJobbyClient>();
        var jobsFactory = provider.GetRequiredService<IJobsFactory>();
        var migrator = provider.GetRequiredService<IJobbyStorageMigrator>();
        Assert.NotNull(server);
        Assert.NotNull(client);
        Assert.NotNull(jobsFactory);
        Assert.NotNull(migrator);

        // hosted service is registered
        var hostedServices = provider.GetServices<IHostedService>().ToList();
        Assert.Contains(hostedServices, x => x is JobbyHostedService);

        // job handler is registered for DI-based execution scope
        var handler = provider.GetRequiredService<IJobCommandHandler<TestJobCommand>>();
        Assert.IsType<TestJobCommandHandler>(handler);

        // server and client are singletons
        Assert.Same(server, provider.GetRequiredService<IJobbyServer>());
        Assert.Same(client, provider.GetRequiredService<IJobbyClient>());
    }

    [Fact]
    public void AddJobbyServerAndClient_WithoutExplicitExecutionScopeFactory_UsesAspNetCoreFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJobbyServerAndClient(cfg =>
        {
            cfg.AddJob<TestJobCommand, TestJobCommandHandler>();
            cfg.ConfigureJobby(jobby =>
            {
                jobby.UseStorage(new Mock<IJobbyStorage>().Object);
                jobby.UsePermanentLocksStorage(_ => new Mock<IPermanentLocksStorage>().Object);
            });
        });
        using var provider = services.BuildServiceProvider();

        // must not throw InvalidBuilderConfigException: scope factory falls back to AspNetCore DI
        var server = provider.GetRequiredService<IJobbyServer>();
        Assert.NotNull(server);
    }

    [Fact]
    public void AddJobbyClient_ResolvesClientAndFactoryButNotServer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJobbyClient(cfg =>
        {
            cfg.ConfigureJobby(jobby =>
            {
                jobby.UseStorage(new Mock<IJobbyStorage>().Object);
            });
        });
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IJobbyClient>());
        Assert.NotNull(provider.GetRequiredService<IJobsFactory>());
        Assert.NotNull(provider.GetRequiredService<IJobbyStorageMigrator>());
        Assert.Throws<InvalidOperationException>(() => { provider.GetRequiredService<IJobbyServer>(); });
    }

    [Fact]
    public async Task AddJobbyServerAndClient_HostedServiceStartsAndStopsResolvedServer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJobbyServerAndClient(cfg =>
        {
            cfg.AddJob<TestJobCommand, TestJobCommandHandler>();
            cfg.ConfigureJobby(jobby =>
            {
                jobby.UseStorage(new Mock<IJobbyStorage>().Object);
                jobby.UsePermanentLocksStorage(_ => new Mock<IPermanentLocksStorage>().Object);
            });
        });
        await using var provider = services.BuildServiceProvider();

        var hostedService = provider.GetServices<IHostedService>().OfType<JobbyHostedService>().Single();

        // full graph functions: server starts its background modules and drains on stop
        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);
    }
}
