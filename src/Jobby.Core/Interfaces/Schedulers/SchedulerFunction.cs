namespace Jobby.Core.Interfaces.Schedulers;

internal delegate DateTime SchedulerFunction(string schedule, ScheduleCalculationContext ctx);