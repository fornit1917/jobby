namespace Jobby.Core.Interfaces.ServerModules.PermanentLockedGroupsCheck;

internal interface IPermanentLocksCheckServerModule
{
    void Start();
    void SendStopSignal();
}