using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobExecutor
{
    Task ExecuteAsync(JobModel job);
}
