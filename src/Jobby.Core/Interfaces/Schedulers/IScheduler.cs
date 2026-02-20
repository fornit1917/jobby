namespace Jobby.Core.Interfaces.Schedulers;

public interface IScheduler
{
    DateTime GetNextStartTime(string schedule, DateTime? previousScheduledStartTime);
}