using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Samples.AspNet.Schedulers;

public class SecondsIntervalSchedule : ISchedule
{
    public uint SecondsInterval { get; init; }
    
    public SecondsIntervalSchedule(uint secondsInterval)
    {
        SecondsInterval = secondsInterval;
    }
}