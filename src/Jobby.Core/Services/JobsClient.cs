using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Core.Services;

public class JobsClient : IJobsClient, IJobsMediator
{
    private readonly IJobsStorage _jobsStorage;
    private readonly IJobParamSerializer _serializer;

    public JobsClient(IJobsStorage jobsStorage, IJobParamSerializer serializer)
    {
        _jobsStorage = jobsStorage;
        _serializer = serializer;
    }

    public long Enqueue(JobModel job)
    {
        job.Status = JobStatus.Scheduled;
        job.CreatedAt = DateTime.UtcNow;
        if (job.ScheduledStartAt == default)
        {
            job.ScheduledStartAt = job.CreatedAt;
        }
        var id = _jobsStorage.Insert(job);
        job.Id = id;
        return job.Id;
    }

    public async Task<long> EnqueueAsync(JobModel job)
    {
        job.Status = JobStatus.Scheduled;
        job.CreatedAt = DateTime.UtcNow;
        if (job.ScheduledStartAt == default)
        {
            job.ScheduledStartAt = job.CreatedAt;
        }
        var id = await _jobsStorage.InsertAsync(job);
        job.Id = id;
        return job.Id;
    }

    public void EnqueueCommand<TCommand>(TCommand command) where TCommand : IJobCommand
    {
        var job = new JobModel
        {
            JobName = TCommand.GetJobName(),
            JobParam = _serializer.SerializeJobParam(command),
        };
        Enqueue(job);
    }

    public Task EnqueueCommandAsync<TCommand>(TCommand command) where TCommand : IJobCommand
    {
        var job = new JobModel
        {
            JobName = TCommand.GetJobName(),
            JobParam = _serializer.SerializeJobParam(command),
        };
        return EnqueueAsync(job);
    }
}
