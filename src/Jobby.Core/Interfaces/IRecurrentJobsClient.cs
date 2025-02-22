namespace Jobby.Core.Interfaces;

public interface IRecurrentJobsClient
{
    Task<long> ScheduleRecurrentAsync(string jobName, string cron);
    long ScheduleRecurrent(string jobName, string cron);

    Task<long> ScheduleRecurrentAsync<T>(string cron) where T : IRecurrentJobHandler;
    long ScheduleRecurrent<T>(string cron) where T : IRecurrentJobHandler;
}
