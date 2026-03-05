namespace Jobby.Core.Models.Schedulers;
public readonly record struct SchedulerExecutionContext(DateTime UtcNow, DateTime PreviousScheduledStartTime);
