using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobExecutionScope : IDisposable
{
    Task ExecuteAsync(JobModel jobModel);
}
