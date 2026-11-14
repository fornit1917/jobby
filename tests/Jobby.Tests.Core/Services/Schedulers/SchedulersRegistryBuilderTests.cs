using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Models;
using Jobby.Core.Services.Schedulers;

namespace Jobby.Tests.Core.Services.Schedulers;

internal class TestSchedule : ISchedule
{
    public string Value { get; init; } = string.Empty;
}

internal class TestScheduleHandlerA : IScheduleHandler<TestSchedule>
{
    public string GetSchedulerTypeName() => TypeName;

    public const string TypeName = "TEST_SCHEDULER_A";

    public string SerializeSchedule(TestSchedule schedule) => schedule.Value;

    public TestSchedule DeserializeSchedule(string schedule) => new TestSchedule { Value = schedule };

    public DateTime GetFirstStartTime(TestSchedule schedule, DateTime utcNow) => utcNow;

    public DateTime GetNextStartTime(TestSchedule schedule, ScheduleCalculationContext ctx) => ctx.UtcNow;
}

internal class TestScheduleHandlerB : IScheduleHandler<TestSchedule>
{
    public string GetSchedulerTypeName() => TestScheduleHandlerA.TypeName;

    public string SerializeSchedule(TestSchedule schedule) => schedule.Value;

    public TestSchedule DeserializeSchedule(string schedule) => new TestSchedule { Value = schedule };

    public DateTime GetFirstStartTime(TestSchedule schedule, DateTime utcNow) => utcNow;

    public DateTime GetNextStartTime(TestSchedule schedule, ScheduleCalculationContext ctx) => ctx.UtcNow;
}

public class SchedulersRegistryBuilderTests
{
    [Fact]
    public void AddScheduler_AddsToRegistry()
    {
        var builder = new SchedulersRegistryBuilder()
            .AddScheduler(new TestScheduleHandlerA());

        var registry = builder.CreateRegistry();

        Assert.NotNull(registry.GetScheduler<TestSchedule>());
        Assert.NotNull(registry.GetSchedulerAsFunction(TestScheduleHandlerA.TypeName));
    }

    [Fact]
    public void AddScheduler_DuplicateScheduleType_ThrowsInvalidBuilderConfigException()
    {
        var builder = new SchedulersRegistryBuilder()
            .AddScheduler(new TestScheduleHandlerA());

        var exception = Assert.Throws<InvalidBuilderConfigException>(
            () => builder.AddScheduler(new TestScheduleHandlerA()));

        Assert.Contains("already been registered", exception.Message);
    }

    [Fact]
    public void AddScheduler_DuplicateSchedulerName_ThrowsInvalidBuilderConfigException()
    {
        var builder = new SchedulersRegistryBuilder()
            .AddScheduler(new TestScheduleHandlerA());

        var exception = Assert.Throws<InvalidBuilderConfigException>(
            () => builder.AddScheduler(new TestScheduleHandlerB()));

        Assert.Contains("already been registered", exception.Message);
    }

    [Fact]
    public void AddScheduler_CronRegisteredTwice_ThrowsInvalidBuilderConfigException()
    {
        var builder = new SchedulersRegistryBuilder()
            .AddScheduler(new CronScheduleHandler());

        Assert.Throws<InvalidBuilderConfigException>(
            () => builder.AddScheduler(new CronScheduleHandler()));
    }
}
