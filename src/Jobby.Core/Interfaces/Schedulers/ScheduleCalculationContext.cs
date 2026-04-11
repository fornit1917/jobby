namespace Jobby.Core.Interfaces.Schedulers;

public readonly record struct ScheduleCalculationContext(DateTime UtcNow, DateTime PrevScheduledTime);