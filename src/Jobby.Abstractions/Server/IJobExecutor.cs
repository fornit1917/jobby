using Jobby.Abstractions.Models;

namespace Jobby.Abstractions.Server;

public interface IJobExecutor
{
    Task ExecuteAsync(JobModel job);
}
