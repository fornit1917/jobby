using System.Diagnostics;
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
        _jobbyServerMock.Verify(x => x.StartBackgroundServiceAsync(), Times.Once);
    }

    [Fact]
    public async Task StopAsync_StopsJoobyServer()
    {
        await _hostedService.StopAsync(CancellationToken.None);
        _jobbyServerMock.Verify(x => x.SendStopSignal(), Times.Once);
    }

    [Fact]
    public async Task StopAsync_WaitsUntilNoInProgressJobs()
    {
        var calls = 0;
        _jobbyServerMock
            .Setup(x => x.HasInProgressJobs())
            .Callback(() => Interlocked.Increment(ref calls))
            .Returns(() => Volatile.Read(ref calls) < 3);

        var stopwatch = Stopwatch.StartNew();
        await _hostedService.StopAsync(CancellationToken.None);
        stopwatch.Stop();

        _jobbyServerMock.Verify(x => x.SendStopSignal(), Times.Once);
        Assert.True(stopwatch.ElapsedMilliseconds >= 100,
            $"StopAsync finished too early: {stopwatch.ElapsedMilliseconds}ms, polling interval is 50ms");
        Assert.True(Volatile.Read(ref calls) >= 3);
    }

    [Fact]
    public async Task StopAsync_HostCancellationRequested_ReturnsImmediatelyWithoutWaiting()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _jobbyServerMock.Setup(x => x.HasInProgressJobs()).Returns(true);

        var stopwatch = Stopwatch.StartNew();
        await _hostedService.StopAsync(cts.Token);
        stopwatch.Stop();

        _jobbyServerMock.Verify(x => x.SendStopSignal(), Times.Once);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, "StopAsync must return immediately on cancelled token");
    }

    [Fact]
    public async Task StopAsync_CancellationDuringDrain_StopsWaiting()
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));
        _jobbyServerMock.Setup(x => x.HasInProgressJobs()).Returns(true);

        var stopwatch = Stopwatch.StartNew();
        await _hostedService.StopAsync(cts.Token);
        stopwatch.Stop();

        _jobbyServerMock.Verify(x => x.SendStopSignal(), Times.Once);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, "Cancelled drain must not hang forever");
        _jobbyServerMock.Verify(x => x.HasInProgressJobs(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task StopAsync_CalledTwice_SendsStopSignalTwice()
    {
        _jobbyServerMock.Setup(x => x.HasInProgressJobs()).Returns(false);

        await _hostedService.StopAsync(CancellationToken.None);
        await _hostedService.StopAsync(CancellationToken.None);

        _jobbyServerMock.Verify(x => x.SendStopSignal(), Times.Exactly(2));
    }
}
