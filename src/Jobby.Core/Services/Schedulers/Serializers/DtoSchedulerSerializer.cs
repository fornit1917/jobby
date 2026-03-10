using System.Diagnostics.CodeAnalysis;

using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Core.Services.Schedulers.Serializers;
public abstract class DtoSchedulerSerializer<TScheduler, TSchedulerDto> : IScheduleSerializer<TScheduler>
{
    private readonly IScheduleSerializer<TSchedulerDto> _dtoSerializer;

    public DtoSchedulerSerializer(IScheduleSerializer<TSchedulerDto>? dtoSerializer = null)
    {
        _dtoSerializer = dtoSerializer ?? new SystemTextJsonSchedulerSerializer<TSchedulerDto>();
    }

    public string Serealize(TScheduler scheduler)
    {
        var dto = ToDto(scheduler);

        return _dtoSerializer.Serealize(dto);
    }

    public bool TryDeserialize(string value, [NotNullWhen(true)] out TScheduler? scheduler)
    {
        if (_dtoSerializer.TryDeserialize(value, out var dto) && TryFromDto(dto, out scheduler))
            return true;
        else
        {
            scheduler = default;
            return false;
        }
    }

    protected abstract TSchedulerDto ToDto(TScheduler scheduler);
    protected abstract bool TryFromDto(TSchedulerDto schedulerDto, [NotNullWhen(true)] out TScheduler? scheduler);
}
