using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Core.Services;

public class RecurrentJobsClient : IRecurrentJobsClient
{
    private readonly IJobsStorage _storage;

    public RecurrentJobsClient(IJobsStorage storage)
    {
        _storage = storage;
    }

    public Guid ScheduleRecurrent(string jobName, string cron)
    {
        var job = CreateRecurrentJob(jobName, cron);
        return _storage.Insert(job);
    }

    public Guid ScheduleRecurrent<T>(string cron) where T : IRecurrentJobHandler
    {
        var job = CreateRecurrentJob(T.GetRecurrentJobName(), cron);
        return _storage.Insert(job);
    }

    public Task<Guid> ScheduleRecurrentAsync(string jobName, string cron)
    {
        var job = CreateRecurrentJob(jobName, cron);
        return _storage.InsertAsync(job);
    }

    public Task<Guid> ScheduleRecurrentAsync<T>(string cron) where T : IRecurrentJobHandler
    {
        var job = CreateRecurrentJob(T.GetRecurrentJobName(), cron);
        return _storage.InsertAsync(job);
    }

    private Job CreateRecurrentJob(string jobName, string cron)
    {
        return new Job
        {
            Id = Guid.NewGuid(),
            JobName = jobName,
            Cron = cron,
            CreatedAt = DateTime.UtcNow,
            Status = JobStatus.Scheduled,
            ScheduledStartAt = CronHelper.GetNext(cron, DateTime.UtcNow),
        };
    }
}
