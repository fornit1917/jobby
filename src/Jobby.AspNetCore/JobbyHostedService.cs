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
        return _jobbyServer.StartBackgroundServiceAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _jobbyServer.SendStopSignal();
        while (!cancellationToken.IsCancellationRequested && _jobbyServer.HasInProgressJobs())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }
}
