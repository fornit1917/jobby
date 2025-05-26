using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

internal interface IJobExecutionService : IDisposable
{
    Task ExecuteJob(Job job, CancellationToken cancellationToken);
}
