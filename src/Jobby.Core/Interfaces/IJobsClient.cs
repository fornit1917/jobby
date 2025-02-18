using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobsClient
{
    Task<long> EnqueueAsync(JobModel job);
    long Enqueue(JobModel job);
}
