namespace Jobby.Core.Interfaces;

public interface IJobsServer
{
    void StartBackgroundService();
    void SendStopSignal();
}
