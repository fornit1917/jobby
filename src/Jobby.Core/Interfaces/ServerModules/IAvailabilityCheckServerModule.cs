namespace Jobby.Core.Interfaces.ServerModules;

internal interface IAvailabilityCheckServerModule
{
    Task AnnounceServer();
    
    void Start();
    void SendStopSignal();
}