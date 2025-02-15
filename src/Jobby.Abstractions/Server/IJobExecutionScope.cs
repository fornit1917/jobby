using Jobby.Abstractions.Models;

namespace Jobby.Abstractions.Server;

public interface IJobExecutionScope : IDisposable
{
    Task ExecuteAsync(JobModel jobModel);
}
