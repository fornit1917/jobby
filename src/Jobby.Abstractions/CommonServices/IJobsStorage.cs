using Jobby.Abstractions.Models;

namespace Jobby.Abstractions.CommonServices;

public interface IJobsStorage
{
    Task<long> InsertAsync(JobModel job);
}
