using Jobby.Core.Interfaces;
using Microsoft.Extensions.Hosting;

namespace Jobby.AspNetCore;

internal class JobbyHostedService : IHostedService
{
    private readonly IJobbyServer _jobbyServer;

    public JobbyHostedService(IJobbyServer jobbyServer)
    {
        _jobbyServer = jobbyServer;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _jobbyServer.StartBackgroundService();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _jobbyServer.SendStopSignal();
        return Task.CompletedTask;
    }
}
