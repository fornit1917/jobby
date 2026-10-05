using System.Globalization;
using Jobby.Core.Models.Dashboard;

namespace Jobby.Dashboard.Client;

internal static class DateTimeFormatPatterns
{
    public static string DatePattern(DashboardDateStyle style) => style switch
    {
        DashboardDateStyle.Iso => "yyyy-MM-dd",
        DashboardDateStyle.DayFirstSlash => "dd/MM/yyyy",
        DashboardDateStyle.DayFirstDot => "dd.MM.yyyy",
        DashboardDateStyle.MonthFirstSlash => "MM/dd/yyyy",
        _ => "yyyy-MM-dd",
    };

    public static string TimePattern(DashboardTimeStyle style) => style switch
    {
        DashboardTimeStyle.H24 => "HH:mm",
        DashboardTimeStyle.H24WithSeconds => "HH:mm:ss",
        DashboardTimeStyle.H12 => "h:mm tt",
        DashboardTimeStyle.H12WithSeconds => "h:mm:ss tt",
        _ => "HH:mm:ss",
    };

    private static string Combined(DashboardDateStyle date, DashboardTimeStyle time)
        => DatePattern(date) + " " + TimePattern(time);

    public static string Format(DateTime local, DashboardDateStyle date, DashboardTimeStyle time)
        => local.ToString(Combined(date, time), CultureInfo.InvariantCulture);
}