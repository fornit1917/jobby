using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

internal interface IJobExecutionService : IDisposable
{
    Task ExecuteJob(JobExecutionModel job, CancellationToken cancellationToken);
}
