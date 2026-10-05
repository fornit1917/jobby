using Jobby.Dashboard.Client.Shared;

namespace Jobby.Tests.Dashboard.Components;

public class ScheduleDisplayTests : MudTestContext
{
    [Fact]
    public void Cron_RendersDescriptionAndBadge()
    {
        var cut = Render<ScheduleDisplay>(p => p
            .Add(c => c.Schedule, "0 0 9 * * *")
            .Add(c => c.SchedulerType, "JOBBY_CRON"));

        Assert.Contains("09:00", cut.Markup);
        Assert.Contains("0 0 9 * * *", cut.Markup);
    }

    [Fact]
    public void Interval_RendersHumanizedDescription()
    {
        var cut = Render<ScheduleDisplay>(p => p
            .Add(c => c.Schedule, """{"Interval":"00:00:45"}""")
            .Add(c => c.SchedulerType, "JOBBY_INTERVAL"));

        Assert.Contains("Every 45 seconds", cut.Markup);
    }

    [Fact]
    public void Custom_RendersRawBadge()
    {
        var cut = Render<ScheduleDisplay>(p => p
            .Add(c => c.Schedule, """{"SecondsInterval":45}""")
            .Add(c => c.SchedulerType, "CUSTOM_SECONDS_INTERVAL"));

        Assert.Contains("SecondsInterval", cut.Markup);
    }

    [Fact]
    public void Empty_RendersDash()
    {
        var cut = Render<ScheduleDisplay>(p => p
            .Add(c => c.Schedule, null)
            .Add(c => c.SchedulerType, "JOBBY_CRON"));

        Assert.Contains("-", cut.Markup);
    }

    [Fact]
    public void Invalid_CronRendersBadgeWithoutDescription()
    {
        var cut = Render<ScheduleDisplay>(p => p
            .Add(c => c.Schedule, "definitely not cron")
            .Add(c => c.SchedulerType, "JOBBY_CRON"));

        Assert.Contains("definitely not cron", cut.Markup);
        Assert.DoesNotContain("jobby-schedule-desc", cut.Markup);
    }
}