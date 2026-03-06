using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;

namespace Jobby.Core.Services.Schedulers.Cron;

internal class CronScheduleSerializer : IJobParamSerializer<CronSchedule>
{
    public string SerializeJobParam(CronSchedule param)
    {
        var dto = new CronScheduleDto(param.CronExpression.ToString(), param.CalculateNextFromPrev);

        return JsonSerializer.Serialize(dto);
    }


    public bool TryDeserializeJobParam(string value, [NotNullWhen(true)] out CronSchedule? param)
    {
        if (JsonSerializer.Deserialize<CronScheduleDto>(value) is { } dto && CronHelper.TryParse(dto.Cron, out var cronExpression))
        {
            param = new CronSchedule(cronExpression, dto.CalculateNextFromPrev);
            return true;
        }
        else
        {
            param = default;
            return false;
        }
    }

    private record CronScheduleDto(string Cron, bool CalculateNextFromPrev);
}