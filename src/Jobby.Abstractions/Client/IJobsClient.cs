using Jobby.Abstractions.Models;

namespace Jobby.Abstractions.Client;

public interface IJobsClient
{
    Task<long> EnqueueAsync(JobModel job);
}
