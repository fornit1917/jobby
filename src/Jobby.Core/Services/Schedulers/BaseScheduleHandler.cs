using System.Text.Json;
using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Core.Services.Schedulers;

public abstract class BaseScheduleHandler<TSchedule> : IScheduleHandler<TSchedule> where TSchedule : ISchedule
{
    public abstract string GetSchedulerTypeName();
    
    public abstract DateTime GetFirstStartTime(TSchedule schedule, DateTime utcNow);
    public abstract DateTime GetNextStartTime(TSchedule schedule, ScheduleCalculationContext ctx);

    public virtual string SerializeSchedule(TSchedule schedule)
    {
        return JsonSerializer.Serialize(schedule);
    }

    public virtual TSchedule DeserializeSchedule(string schedule)
    {
        TSchedule? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<TSchedule>(schedule);
        }
        catch (Exception e)
        {
            throw new InvalidScheduleException("Could not parse schedule from text format", e);
        }

        if (parsed == null)
        {
            throw new InvalidScheduleException("Could not parse schedule from text format");
        }
        
        return parsed;
    }
}