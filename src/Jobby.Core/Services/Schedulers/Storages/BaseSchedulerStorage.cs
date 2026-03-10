using System.Text.Json;

using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Services.Schedulers.Serializers;

namespace Jobby.Core.Services.Schedulers.Storages;
public abstract class BaseSchedulerStorage<TScheduler> : ISchedulerStorage<TScheduler>
    where TScheduler : IScheduler
{
    public abstract string DefaultSchedulerType { get; }
    public virtual IScheduleSerializer<TScheduler> Serializer { get; }

    public BaseSchedulerStorage(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        Serializer = new SystemTextJsonSchedulerSerializer<TScheduler>(jsonSerializerOptions);
    }
}
