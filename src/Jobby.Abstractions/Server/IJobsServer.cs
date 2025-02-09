namespace Jobby.Abstractions.Server;

public interface IJobsServer
{
    void StartBackgroundService();
    void SendStopSignal();
}
