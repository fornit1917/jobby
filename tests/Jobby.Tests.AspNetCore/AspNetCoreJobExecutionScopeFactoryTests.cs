using Jobby.AspNetCore;
using Jobby.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Jobby.Tests.AspNetCore;

public class AspNetCoreJobExecutionScopeFactoryTests
{
    private readonly Mock<IServiceScope> _containerScopeMock;
    private readonly Mock<IServiceScopeFactory> _containerScopeFactoryMock;
    private readonly Mock<IServiceProvider> _containerMock;

    private AspNetCoreJobExecutionScopeFactory _scopeFactory;

    public AspNetCoreJobExecutionScopeFactoryTests()
    {
        _containerScopeMock = new Mock<IServiceScope>();
        
        _containerScopeFactoryMock = new Mock<IServiceScopeFactory>();
        _containerScopeFactoryMock.Setup(x => x.CreateScope()).Returns(_containerScopeMock.Object);
        
        _containerMock = new Mock<IServiceProvider>();
        _containerMock
            .Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(_containerScopeFactoryMock.Object);

        _scopeFactory = new(_containerMock.Object);
    }

    [Fact]
    public void CreateJobExecutionScope_CreatesJobExecutionScopeBasedOnContainer()
    {
        var scope = _scopeFactory.CreateJobExecutionScope();

        Assert.NotNull(scope);
        Assert.IsType<AspNetCoreJobExecutionScope>(scope);
        _containerScopeFactoryMock.Verify(x => x.CreateScope(), Times.Once);
    }

}
