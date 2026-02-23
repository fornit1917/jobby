namespace Jobby.Core.Interfaces.ServerModules;

internal interface IAvailabilityCheckServerModule
{
    void Start();
    void SendStopSignal();
}