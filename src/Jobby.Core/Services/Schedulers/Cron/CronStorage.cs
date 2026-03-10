using System.Diagnostics.CodeAnalysis;

using Jobby.Core.Helpers;

namespace Jobby.Core.Services.Schedulers.Cron;

internal class CronStorage : BaseDtoSchedulerStorage<CronSchedule, CronScheduleDto>
{
    public override string DefaultSchedulerType => "CRON";

    protected override CronScheduleDto ToDto(CronSchedule scheduler) => new CronScheduleDto(scheduler.CronExpression.ToString(), scheduler.CalculateNextFromPrev);

    protected override bool TryFromDto(CronScheduleDto dto, [NotNullWhen(true)] out CronSchedule? scheduler)
    {
        if (CronHelper.TryParse(dto.Cron, out var cronExpression))
        {
            scheduler = new CronSchedule(cronExpression, dto.CalculateNextFromPrev);
            return true;
        }
        else
        {
            scheduler = default;
            return false;
        }
    }

    
}
internal record CronScheduleDto(string Cron, bool CalculateNextFromPrev);