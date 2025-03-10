namespace Jobby.Core.Interfaces;

public interface IRecurrentJobsClient
{
    Task<Guid> ScheduleRecurrentAsync(string jobName, string cron);
    Guid ScheduleRecurrent(string jobName, string cron);

    Task<Guid> ScheduleRecurrentAsync<T>(string cron) where T : IRecurrentJobHandler;
    Guid ScheduleRecurrent<T>(string cron) where T : IRecurrentJobHandler;
}
