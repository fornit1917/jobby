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
        _storage.Insert(job);
        return job.Id;
    }

    public Guid ScheduleRecurrent<T>(string cron) where T : IRecurrentJobHandler
    {
        var job = CreateRecurrentJob(T.GetRecurrentJobName(), cron);
        _storage.Insert(job);
        return job.Id;
    }

    public async Task<Guid> ScheduleRecurrentAsync(string jobName, string cron)
    {
        var job = CreateRecurrentJob(jobName, cron);
        await _storage.InsertAsync(job);
        return job.Id;
    }

    public async Task<Guid> ScheduleRecurrentAsync<T>(string cron) where T : IRecurrentJobHandler
    {
        var job = CreateRecurrentJob(T.GetRecurrentJobName(), cron);
        await _storage.InsertAsync(job);
        return job.Id;
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
