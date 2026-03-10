using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Services.Schedulers.Serializers;

namespace Jobby.Core.Services;
internal abstract class BaseDtoSchedulerStorage<TScheduler, TSchedulerDto> : DtoSchedulerSerializer<TScheduler, TSchedulerDto>, ISchedulerStorage<TScheduler>
    where TScheduler : IScheduler
{
    public abstract string DefaultSchedulerType { get; }
    public virtual IScheduleSerializer<TScheduler> Serializer => this;

    public BaseDtoSchedulerStorage(IScheduleSerializer<TSchedulerDto> dtoSerializer)
        : base(dtoSerializer)
    { }
}
