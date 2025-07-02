using Jobby.AspNetCore;
using Jobby.Core.Interfaces;
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
    public void AddJobbyClient_AddsJobbyClientAndFactoryAsSingletons()
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
    public void AddJobby_AddsJobHandlersAndJobbyServices()
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
