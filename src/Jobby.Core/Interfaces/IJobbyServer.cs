namespace Jobby.Core.Interfaces;

public interface IJobbyServer
{
    void StartBackgroundService();
    void SendStopSignal();

    IReadOnlyList<int> BatchCompletionStat {  get; }
}
