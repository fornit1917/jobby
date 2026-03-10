using System.Diagnostics.CodeAnalysis;

using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Core.Services.Schedulers.CronSimple;

internal class CronSimpleSchedulerSerializer : IScheduleSerializer<CronSimpleScheduler>
{
    public string Serealize(CronSimpleScheduler scheduler)
    {
        var res = scheduler.CronExpression.ToString();
        return res;
    }

    public bool TryDeserialize(string value, [NotNullWhen(true)] out CronSimpleScheduler? scheduler)
    {
        if (!CronHelper.TryParse(value, out var cronExpression))
        {
            scheduler = default;
            return false;
        }
        else
        {
            scheduler = new CronSimpleScheduler(cronExpression);
            return true;
        }
    }
}