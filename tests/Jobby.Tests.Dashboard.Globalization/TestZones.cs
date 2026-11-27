namespace Jobby.Tests.Dashboard.Globalization;

// Test zones for the display engine. Under InvariantGlobalization the OS of a Windows machine cannot
// resolve IANA ids (that mapping lives in the disabled ICU), while Linux resolves only IANA ids, so:
// - the id-resolution tests pick a zone id the current OS can resolve (TestZones.TokyoId/LondonId);
// - the offset, fold and gap tests use custom zones with explicit rules: they are built without the
//   OS database, behave identically on every OS and never change when real-world DST rules change.
internal static class TestZones
{
    public static readonly TimeZoneInfo FixedUtcPlusOne = TimeZoneInfo.CreateCustomTimeZone(
        "Test/FixedPlusOne", TimeSpan.FromHours(1), "Test +1", "Test +1");

    // UTC+0 from November to March 1, UTC+1 from March 1 (00:00) to November 1 (00:00).
    public static readonly TimeZoneInfo WinterSummerUtcPlusOne = Create(
        "Test/WinterSummer",
        ruleStart: new DateTime(1, 1, 1, 0, 0, 0), startMonth: 3, startDay: 1,
        ruleEnd: new DateTime(1, 1, 1, 0, 0, 0), endMonth: 11, endDay: 1);

    // UTC+0 with a +1 rule from March 29 (02:00): the wall times 02:00-03:00 on March 29 do not exist.
    public static readonly TimeZoneInfo SpringGapZone = Create(
        "Test/SpringGap",
        ruleStart: new DateTime(1, 1, 1, 2, 0, 0), startMonth: 3, startDay: 29,
        ruleEnd: new DateTime(1, 1, 1, 0, 0, 0), endMonth: 10, endDay: 1);

    // UTC+0 with a +1 rule ending October 25 (02:00): the wall times 01:00-02:00 happen twice.
    public static readonly TimeZoneInfo FallFoldZone = Create(
        "Test/FallFold",
        ruleStart: new DateTime(1, 1, 1, 0, 0, 0), startMonth: 3, startDay: 1,
        ruleEnd: new DateTime(1, 1, 1, 2, 0, 0), endMonth: 10, endDay: 25);

    // A DST gap that starts at midnight on March 1: the lower bound of a range must stay on March 1.
    public static readonly TimeZoneInfo MidnightGapZone = Create(
        "Test/MidnightGap",
        ruleStart: new DateTime(1, 1, 1, 0, 0, 0), startMonth: 3, startDay: 1,
        ruleEnd: new DateTime(1, 1, 1, 0, 0, 0), endMonth: 11, endDay: 1);

    public static string TokyoId => Resolvable("Asia/Tokyo", "Tokyo Standard Time");

    public static string LondonId => Resolvable("Europe/London", "GMT Standard Time");

    private static TimeZoneInfo Create(string id, DateTime ruleStart, int startMonth, int startDay,
        DateTime ruleEnd, int endMonth, int endDay)
    {
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2000, 1, 1), new DateTime(2100, 12, 31), TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(ruleStart, startMonth, startDay),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(ruleEnd, endMonth, endDay));
        return TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.Zero, id, id, id, [rule]);
    }

    private static string Resolvable(params string[] ids) =>
        ids.First(id => TimeZoneInfo.TryFindSystemTimeZoneById(id, out _));
}
