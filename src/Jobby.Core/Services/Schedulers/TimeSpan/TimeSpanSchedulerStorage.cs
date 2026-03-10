using System.Diagnostics.CodeAnalysis;

namespace Jobby.Core.Services.Schedulers.TimeSpans;

internal class TimeSpanStorage : BaseDtoSchedulerStorage<TimeSpanScheduler, TimeSpanSchedulerDto>
{
    public override string DefaultSchedulerType => "TIME_SPAN";

    protected override TimeSpanSchedulerDto ToDto(TimeSpanScheduler scheduler) => new TimeSpanSchedulerDto(scheduler.Interval, scheduler.CalculateNextFromPrev);

    protected override bool TryFromDto(TimeSpanSchedulerDto dto, [NotNullWhen(true)] out TimeSpanScheduler? scheduler)
    {
        if (dto.Interval > TimeSpan.Zero)
        {
            scheduler = new TimeSpanScheduler(dto.Interval, dto.CalculateNextFromPrev);
            return true;
        }
        else
        {
            scheduler = default;
            return false;
        }
    }
}
internal record TimeSpanSchedulerDto(TimeSpan Interval, bool CalculateNextFromPrev);