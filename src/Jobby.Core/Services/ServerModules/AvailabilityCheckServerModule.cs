using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.ServerModules;
using Jobby.Core.Models;
using Microsoft.Extensions.Logging;

namespace Jobby.Core.Services.ServerModules;

internal class AvailabilityCheckServerModule : IAvailabilityCheckServerModule
{
    private readonly IJobbyStorage _storage;
    private readonly ITimerService _timer;
    private readonly ILogger<AvailabilityCheckServerModule> _logger;
    private readonly JobbyServerSettings _settings;
    private readonly string _serverId;

    private CancellationTokenSource _cancellationTokenSource;

    public AvailabilityCheckServerModule(IJobbyStorage storage,
        ITimerService timer,
        ILogger<AvailabilityCheckServerModule> logger,
        JobbyServerSettings settings,
        string serverId)
    {
        _storage = storage;
        _timer = timer;
        _logger = logger;
        _settings = settings;
        _serverId = serverId;
        _cancellationTokenSource = new CancellationTokenSource();
    }
    
    public void Start()
    {
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }
        
        _ = Task.Run(() => SendHeartbeatAndProcessLostServers(_cancellationTokenSource.Token));
    }

    public void SendStopSignal()
    {
        _cancellationTokenSource.Cancel();
    }
    
    private async Task SendHeartbeatAndProcessLostServers(CancellationToken cancellationToken)
    {
        List<string> deletedServerIds = new List<string>();
        List<StuckJobModel> stuckJobs = new List<StuckJobModel>();

        while (!cancellationToken.IsCancellationRequested)
        {
            // send heartbeat
            try
            {
                await _storage.SendHeartbeatAsync(_serverId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during send heartbeat");
            }

            // detect lost servers and restart their jobs
            try
            {
                var minLastHeartbeat = DateTime.UtcNow.AddSeconds(-1 * _settings.MaxNoHeartbeatIntervalSeconds);
                await _storage.DeleteLostServersAndRestartTheirJobsAsync(minLastHeartbeat, deletedServerIds, stuckJobs);
                foreach (var serverId in deletedServerIds)
                {
                    _logger.LogInformation("Lost server was found and deleted, serverId = {ServerId}", serverId);
                }
                foreach (var job in stuckJobs)
                {
                    if (job.CanBeRestarted)
                    {
                        _logger.LogInformation(
                            "Job was restarted because server did not send hearbeat, jobName = {JobName}, id = {JobId}",
                            job.JobName, job.Id);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Probably job got stuck and can not be restarted automatically, its server did not send heartbeat, jobName = {JobName}, id = {JobId}",
                            job.JobName, job.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during detect lost servers and restart their jobs");
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await _timer.Delay(TimeSpan.FromSeconds(_settings.HeartbeatIntervalSeconds), cancellationToken);
            }
        }
    }
}