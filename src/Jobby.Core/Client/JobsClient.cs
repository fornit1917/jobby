using Jobby.Abstractions.Client;
using Jobby.Abstractions.CommonServices;
using Jobby.Abstractions.Models;

namespace Jobby.Core.Client;

public class JobsClient : IJobsClient
{
    private readonly IJobsStorage _jobsStorage;

    public JobsClient(IJobsStorage jobsStorage)
    {
        _jobsStorage = jobsStorage;
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
}
