namespace Jobby.Core.Interfaces;

public interface IJobbyServer
{
    [Obsolete("Use StartBackgroundServiceAsync")]
    void StartBackgroundService();
    
    Task StartBackgroundServiceAsync();
    void SendStopSignal();
    bool HasInProgressJobs();
}
