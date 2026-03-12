namespace Jobby.Core.Interfaces.Schedulers;

public interface IScheduleHandler<TSchedule> where TSchedule : ISchedule
{
    string GetSchedulerTypeName();
    
    string SerializeSchedule(TSchedule schedule);
    TSchedule DeserializeSchedule(string schedule);
    
    DateTime GetFirstStartTime(TSchedule schedule, DateTime utcNow); 
    DateTime GetNextStartTime(TSchedule schedule, ScheduleCalculationContext ctx);
}