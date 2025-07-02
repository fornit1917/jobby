using Jobby.AspNetCore;
using Jobby.Core.Interfaces;
using Jobby.TestsUtils.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Jobby.Tests.AspNetCore;

public class AspNetCoreJobExecutionScopeTests
{
    private readonly Mock<IServiceScope> _containerScopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;

    private readonly AspNetCoreJobExecutionScope _scope;

    public AspNetCoreJobExecutionScopeTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _containerScopeMock = new Mock<IServiceScope>();
        _containerScopeMock.SetupGet(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);

        _scope = new AspNetCoreJobExecutionScope(_containerScopeMock.Object);
    }

    [Fact]
    public void GetService_ReturnsServiceCreatedByContainerScope()
    {
        var expectedService = new object();
        var requestedServiceType = typeof(IJobCommandHandler<TestJobCommand>);
        _serviceProviderMock.Setup(x => x.GetService(requestedServiceType)).Returns(expectedService);

        var actualService = _scope.GetService(requestedServiceType);

        Assert.Equal(expectedService, actualService);
    }

    [Fact]
    public void Dispose_CallsDisposeForContainerScope()
    {
        _scope.Dispose();

        _containerScopeMock.Verify(x => x.Dispose(), Times.Once);
    }
}