using System.Diagnostics.CodeAnalysis;

using Cronos;

using Jobby.Core.Exceptions;

namespace Jobby.Core.Helpers;

public static class CronHelper
{
    public static DateTime GetNext(this CronExpression cronExpression, DateTime from)
        => cronExpression.GetNextOccurrence(from) ??
        throw new InvalidScheduleException($"Could not calculate next occurence by cron expression '{cronExpression}' from {from}");

    public static CronExpression Parse(string cron)
    {
        return TryParse(cron, out var cronExpression) ?
            cronExpression :
            throw new ArgumentException($"{nameof(cron)} has invalid cron format: {cron}");
    }

    public static bool TryParse(string cron, [NotNullWhen(true)] out CronExpression? cronExpression)
    {
        var format = GetFormat(cron);
        return CronExpression.TryParse(cron, format, out cronExpression);
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
