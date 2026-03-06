using System.Diagnostics.CodeAnalysis;

using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;

namespace Jobby.Core.Services.Schedulers.CronSimple;

internal class CronSimpleScheduleSerializer : IJobParamSerializer<CronSimpleSchedule>
{
    public string SerializeJobParam(CronSimpleSchedule param)
    {
        var res = param.CronExpression.ToString();
        return res;
    }

    public bool TryDeserializeJobParam(string value, [NotNullWhen(true)] out CronSimpleSchedule? param)
    {
        if (!CronHelper.TryParse(value, out var cronExpression))
        {
            param = default;
            return false;
        }
        else
        {
            param = new CronSimpleSchedule(cronExpression);
            return true;
        }
    }
}