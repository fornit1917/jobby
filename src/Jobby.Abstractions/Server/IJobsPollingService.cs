namespace Jobby.Abstractions.Server;

public interface IJobsPollingService
{
    void StartBackgroundService();
    void SendStopSignal();
}
