using Cronos;
using Jobby.Core.Exceptions;

namespace Jobby.Core.Helpers;

internal static class CronHelper
{
    public static DateTime GetNext(string cron, DateTime from)
    {
        var format = GetFormat(cron);
        if (!CronExpression.TryParse(cron, format, out var parsedCron))
        {
            throw new CronException($"Could not parse cron expression '{cron}'");
        }
        var next = parsedCron.GetNextOccurrence(from);
        if (!next.HasValue)
        {
            throw new CronException($"Could not calculate next occurence by cron expression '{cron}'");
        }
        return next.Value;
    }

    private static CronFormat GetFormat(string cron)
    {
        if (cron.Length > 0)
        {
            var componentsCount = 0;
            for (int i = 0; i < cron.Length - 1; i++)
            {
                if (cron[i] != ' ' && cron [i + 1]  == ' ')
                {
                    componentsCount++;
                }
            }
            if (cron[cron.Length - 1] != ' ')
            {
                componentsCount++;
            }
            if (componentsCount == 6)
            {
                return CronFormat.IncludeSeconds;
            }
        }
        
        return CronFormat.Standard;
    }
}
