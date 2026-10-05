using System.Globalization;
using System.Text.Json;
using CronExpressionDescriptor;

namespace Jobby.Dashboard.Client.Shared;

public enum ScheduleKind
{
    None,
    Cron,
    Interval,
    Custom,
}

public sealed record ScheduleDisplayModel
{
    public required ScheduleKind Kind { get; init; }

    public string? Description { get; init; }

    public string? Badge { get; init; }
}

public static class ScheduleFormatter
{
    private const string CronType = "JOBBY_CRON";
    private const string IntervalType = "JOBBY_INTERVAL";

    private static readonly Options CronOptions = new()
    {
        ThrowExceptionOnParseError = true,
    };

    public static ScheduleDisplayModel Format(string? schedule, string? schedulerType)
    {
        if (string.IsNullOrWhiteSpace(schedule))
        {
            return new ScheduleDisplayModel { Kind = ScheduleKind.None };
        }

        var trimmed = schedule.Trim();
        return schedulerType switch
        {
            CronType => FormatCron(trimmed),
            IntervalType => FormatInterval(trimmed),
            _ => new ScheduleDisplayModel { Kind = ScheduleKind.Custom, Badge = trimmed },
        };
    }

    private static ScheduleDisplayModel FormatCron(string schedule)
    {
        var cron = ExtractCronExpression(schedule);
        string? description = null;
        try
        {
            description = ExpressionDescriptor.GetDescription(cron, CronOptions);
        }
        catch (Exception)
        {
            // ignored
        }

        return new ScheduleDisplayModel { Kind = ScheduleKind.Cron, Description = description, Badge = cron };
    }

    private static string ExtractCronExpression(string schedule)
    {
        if (!schedule.StartsWith('{'))
        {
            return schedule;
        }

        try
        {
            using var doc = JsonDocument.Parse(schedule);
            if (doc.RootElement.TryGetProperty("CronExpression", out var expr)
                && expr.ValueKind == JsonValueKind.String)
            {
                return expr.GetString() ?? schedule;
            }
        }
        catch (JsonException)
        {
        }

        return schedule;
    }

    private static ScheduleDisplayModel FormatInterval(string schedule)
    {
        var interval = ParseInterval(schedule);
        if (interval is null)
        {
            return new ScheduleDisplayModel { Kind = ScheduleKind.Interval, Badge = schedule };
        }

        var badge = interval.Value.ToString();
        if (interval.Value < TimeSpan.Zero)
        {
            return new ScheduleDisplayModel { Kind = ScheduleKind.Interval, Badge = badge };
        }

        return new ScheduleDisplayModel
        {
            Kind = ScheduleKind.Interval,
            Description = HumanizeInterval(interval.Value),
            Badge = badge,
        };
    }

    private static TimeSpan? ParseInterval(string schedule)
    {
        if (schedule.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(schedule);
                if (doc.RootElement.TryGetProperty("Interval", out var prop)
                    && prop.ValueKind == JsonValueKind.String
                    && TimeSpan.TryParse(prop.GetString(), CultureInfo.InvariantCulture, out var fromJson))
                {
                    return fromJson;
                }
            }
            catch (JsonException)
            {
            }

            return null;
        }

        return TimeSpan.TryParse(schedule, CultureInfo.InvariantCulture, out var bare) ? bare : null;
    }

    private static string HumanizeInterval(TimeSpan interval)
    {
        if (interval == TimeSpan.Zero)
        {
            return "Every 0 seconds";
        }

        var parts = new List<string>(5);
        if (interval.Days > 0)
        {
            parts.Add($"{interval.Days} {Plural(interval.Days, "day")}");
        }

        if (interval.Hours > 0)
        {
            parts.Add($"{interval.Hours} {Plural(interval.Hours, "hour")}");
        }

        if (interval.Minutes > 0)
        {
            parts.Add($"{interval.Minutes} {Plural(interval.Minutes, "minute")}");
        }

        if (interval.Seconds > 0)
        {
            parts.Add($"{interval.Seconds} {Plural(interval.Seconds, "second")}");
        }

        if (interval.Milliseconds > 0)
        {
            parts.Add($"{interval.Milliseconds} {Plural(interval.Milliseconds, "millisecond")}");
        }

        return parts.Count == 0 ? "Every <1 millisecond" : "Every " + string.Join(" ", parts);
    }

    private static string Plural(int value, string unit) => value == 1 ? unit : unit + "s";
}