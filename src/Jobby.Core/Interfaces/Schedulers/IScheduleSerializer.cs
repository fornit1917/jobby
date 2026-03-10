using System.Diagnostics.CodeAnalysis;

namespace Jobby.Core.Interfaces.Schedulers;
public interface IScheduleSerializer<TScheduler>
{
    string Serealize(TScheduler scheduler);
    bool TryDeserialize(string value, [NotNullWhen(true)] out TScheduler? scheduler);
}
