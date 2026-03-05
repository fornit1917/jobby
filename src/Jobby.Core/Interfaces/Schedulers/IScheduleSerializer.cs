using System.Diagnostics.CodeAnalysis;

namespace Jobby.Core.Interfaces.Schedulers;

public interface IScheduleSerializer<TSchedule> where TSchedule : ISchedule
{
    bool TryDeserialize(string value, [NotNullWhen(true)] out TSchedule? schedule);
    string Serialize(TSchedule schedule);
}