using Jobby.AspNetCore;
using Jobby.Core.Interfaces;
using Moq;

namespace Jobby.Tests.AspNetCore;

public class JobbyHostedServiceTests
{
    private readonly Mock<IJobbyServer> _jobbyServerMock;

    private readonly JobbyHostedService _hostedService;

    public JobbyHostedServiceTests()
    {
        _jobbyServerMock = new Mock<IJobbyServer>();
        _hostedService = new JobbyHostedService(_jobbyServerMock.Object);
    }

    [Fact]
    public async Task StartAsync_StartsJobbyServer()
    {
        await _hostedService.StartAsync(CancellationToken.None);
        _jobbyServerMock.Verify(x => x.StartBackgroundService(), Times.Once);
    }

    [Fact]
    public async Task StopAsync_StopsJoobyServer()
    {
        await _hostedService.StopAsync(CancellationToken.None);
        _jobbyServerMock.Verify(x => x.SendStopSignal(), Times.Once);
    }
}
