using System.Diagnostics.CodeAnalysis;

using Jobby.Core.Helpers;

namespace Jobby.Core.Services.Schedulers.Cron;

internal class CronStorage : BaseDtoSchedulerStorage<CronScheduler, CronScheduleDto>
{
    public override string DefaultSchedulerType => "CRON";

    protected override CronScheduleDto ToDto(CronScheduler scheduler) => new CronScheduleDto(scheduler.CronExpression.ToString(), scheduler.CalculateNextFromPrev);

    protected override bool TryFromDto(CronScheduleDto dto, [NotNullWhen(true)] out CronScheduler? scheduler)
    {
        if (CronHelper.TryParse(dto.Cron, out var cronExpression))
        {
            scheduler = new CronScheduler(cronExpression, dto.CalculateNextFromPrev);
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